using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NCVizDash.Core.Abstractions;
using NCVizDash.Core.Analytics;
using NCVizDash.Models;

namespace NCVizDash.TaskPane.Services;

/// <summary>
/// Decorator over any <see cref="IAnalyticsEngine"/> that caches
/// <c>QueryAsync(QuerySpec)</c> results for a short TTL, keyed by a hash of the
/// spec's JSON representation. Two widgets with identical field mappings and
/// filters (a common case — e.g. two KPIs on the same measure with the same
/// global filters active) hit DuckDB once instead of twice. Cache entries are
/// invalidated wholesale whenever a data source is (re)loaded or unloaded, since
/// stale cached rows would silently show outdated numbers otherwise — correctness
/// takes priority over cache hit rate.
/// </summary>
public sealed class CachingAnalyticsEngine : IAnalyticsEngine
{
    private readonly IAnalyticsEngine _inner;
    private readonly ILogger<CachingAnalyticsEngine> _logger;
    private readonly Dictionary<string, (DateTimeOffset ExpiresAt, IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows)> _cache = new();
    private readonly object _lock = new();
    private readonly TimeSpan _ttl;

    public CachingAnalyticsEngine(IAnalyticsEngine inner, ILogger<CachingAnalyticsEngine> logger, TimeSpan? ttl = null)
    {
        _inner = inner;
        _logger = logger;
        _ttl = ttl ?? TimeSpan.FromSeconds(15);
    }

    public async Task LoadDataSourceAsync(
        DataSourceDescriptor descriptor, IReadOnlyList<IReadOnlyDictionary<string, object?>> rows, CancellationToken ct = default)
    {
        await _inner.LoadDataSourceAsync(descriptor, rows, ct);
        InvalidateAll("data source loaded/reloaded");
    }

    public Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(string sql, CancellationToken ct = default) =>
        _inner.QueryAsync(sql, ct); // raw-SQL path bypasses the cache — used for ad-hoc/administrative queries only

    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(QuerySpec spec, CancellationToken ct = default)
    {
        var key = HashOf(spec);

        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
            {
                _logger.LogDebug("Query cache hit ({Key}).", key.Substring(0, 8));
                return cached.Rows;
            }
        }

        var rows = await _inner.QueryAsync(spec, ct);

        lock (_lock)
        {
            _cache[key] = (DateTimeOffset.UtcNow.Add(_ttl), rows);
        }

        return rows;
    }

    public async Task UnloadDataSourceAsync(Guid dataSourceId, CancellationToken ct = default)
    {
        await _inner.UnloadDataSourceAsync(dataSourceId, ct);
        InvalidateAll("data source unloaded");
    }

    public string? GetTableName(Guid dataSourceId) => _inner.GetTableName(dataSourceId);

    private void InvalidateAll(string reason)
    {
        lock (_lock)
        {
            var count = _cache.Count;
            _cache.Clear();
            if (count > 0)
                _logger.LogDebug("Query cache invalidated ({Count} entr{Suffix}) — {Reason}.",
                    count, count == 1 ? "y" : "ies", reason);
        }
    }

    private static string HashOf(QuerySpec spec)
    {
        var json = JsonSerializer.Serialize(spec);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes);
    }
}
