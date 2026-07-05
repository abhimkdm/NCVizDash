using Microsoft.Extensions.Logging;
using NCVizDash.Core.Abstractions;
using NCVizDash.Core.Analytics;

namespace NCVizDash.TaskPane.Services;

/// <summary>
/// Retrieves the distinct values of any field on any loaded data source, entirely
/// generically — no hardcoded field names or business domains. This is what lets
/// the global filter bar offer a real, data-driven value picker for whatever
/// dimensions happen to exist in the user's workbook (Department, Region, Project,
/// or anything else), rather than a fixed preset list.
/// </summary>
public sealed class DistinctValueService
{
    private readonly IAnalyticsEngine _analyticsEngine;
    private readonly ILogger<DistinctValueService> _logger;

    private const int MaxDistinctValues = 200;

    /// <summary>Initialises the service.</summary>
    public DistinctValueService(IAnalyticsEngine analyticsEngine, ILogger<DistinctValueService> logger)
    {
        _analyticsEngine = analyticsEngine;
        _logger = logger;
    }

    /// <summary>
    /// Returns up to <see cref="MaxDistinctValues"/> distinct, non-empty values for
    /// the given field on the given data source, sorted ascending. Returns an empty
    /// list (never throws) if the data source isn't loaded or the field has no data.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetDistinctValuesAsync(
        Guid dataSourceId, string fieldName, CancellationToken ct = default)
    {
        var tableName = _analyticsEngine.GetTableName(dataSourceId);
        if (tableName is null || string.IsNullOrWhiteSpace(fieldName))
            return [];

        try
        {
            // A GROUP BY with no aggregated measure naturally yields distinct values
            // of the dimension — no separate "SELECT DISTINCT" code path needed.
            var spec = new QuerySpec
            {
                TableName = tableName,
                Dimensions = [fieldName],
                SortField = fieldName,
                Limit = MaxDistinctValues
            };

            var rows = await _analyticsEngine.QueryAsync(spec, ct);
            var sanitisedField = SqlFilterTranslator.SanitiseColumnName(fieldName);

            return rows
                .Select(r => r.TryGetValue(sanitisedField, out var v) ? v?.ToString() : null)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch distinct values for '{Field}' on data source {DataSourceId}.",
                fieldName, dataSourceId);
            return [];
        }
    }
}
