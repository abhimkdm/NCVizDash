using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using NCVizDash.Core.Abstractions;
using NCVizDash.Core.Classification;
using NCVizDash.Models;

namespace NCVizDash.Connectors.Sql;

/// <summary>
/// Connects to SQL Server. <paramref name="connectionInfo"/> is a standard
/// ADO.NET connection string. <c>DiscoverAsync</c> here treats the *entire
/// connection string* as identifying a single logical data source named after
/// the query — callers are expected to pair a connection string with a specific
/// query via <see cref="DiscoverTable"/> or a raw SQL query, since "discover
/// everything in this database" isn't a well-defined single `DataSourceDescriptor`.
/// </summary>
public sealed class SqlServerConnector : IDataConnector
{
    private readonly ILogger<SqlServerConnector> _logger;

    /// <inheritdoc/>
    public string ConnectorType => "sqlserver";

    /// <summary>Initializes a new instance of the <see cref="SqlServerConnector"/> class.</summary>
    /// <param name="logger">Logger used for diagnostic output.</param>
    public SqlServerConnector(ILogger<SqlServerConnector> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Discovers a data source by running a user-supplied query and inspecting its
    /// result schema. <paramref name="connectionInfo"/> must be a connection string;
    /// pass the query itself in <see cref="DiscoverTable"/>'s <c>sql</c> parameter —
    /// this overload exists to satisfy <see cref="IDataConnector"/> and defaults to
    /// <c>SELECT TOP 1000 * FROM information_schema.tables</c>-style discovery being
    /// the caller's responsibility (call <see cref="DiscoverTable"/> directly instead).
    /// </summary>
    public async Task<IReadOnlyList<DataSourceDescriptor>> DiscoverAsync(string connectionInfo, CancellationToken ct = default)
    {
        _logger.LogWarning(
            "SqlServerConnector.DiscoverAsync called without an explicit query — " +
            "use DiscoverTable(connectionString, tableName) or DiscoverQuery(connectionString, sql, name) instead.");
        return [];
    }

    /// <summary>Discovers a data source from a specific table (schema-qualified, e.g. "dbo.Sales").</summary>
    public Task<DataSourceDescriptor> DiscoverTable(string connectionString, string tableName, CancellationToken ct = default) =>
        DiscoverQuery(connectionString, $"SELECT * FROM {tableName}", tableName, ct);

    /// <summary>Discovers a data source from an arbitrary SELECT query, named by the caller.</summary>
    public async Task<DataSourceDescriptor> DiscoverQuery(string connectionString, string sql, string name, CancellationToken ct = default)
    {
        var rows = await ExecuteQueryAsync(connectionString, sql, ct);

        var descriptor = new DataSourceDescriptor
        {
            Name = name,
            SourceType = "SqlServer",
            SheetName = string.Empty,
            RowCount = rows.Count
        };

        if (rows.Count > 0)
        {
            foreach (var header in rows[0].Keys)
            {
                var sample = rows.Take(25).Select(r => r.TryGetValue(header, out var v) ? v : null);
                descriptor.Fields.Add(new FieldDescriptor
                {
                    Name = header,
                    DisplayName = header,
                    ClrType = sample.FirstOrDefault(v => v is not null)?.GetType().FullName ?? "System.Object",
                    FieldType = FieldTypeClassifier.ClassifyFromSample(header, sample)
                });
            }
        }

        return descriptor;
    }

    /// <summary>
    /// Reads rows via a composite <c>connectionInfo</c> string built as
    /// <c>"&lt;connectionString&gt;||&lt;sql&gt;"</c>, since <see cref="IDataConnector"/>'s
    /// contract only carries one string.
    /// </summary>
    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ReadRowsAsync(
        DataSourceDescriptor descriptor, string connectionInfo, CancellationToken ct = default)
    {
        // connectionInfo is expected to be "<connectionString>||<sql>" here since
        // IDataConnector's contract only carries one string — callers built via
        // DiscoverTable/DiscoverQuery should keep the query alongside the descriptor
        // themselves (e.g. in a wrapping view-model) rather than relying on this
        // generic re-entry path, which is a reasonable simplification for Phase 14's
        // scope. See CsvFileConnector/JsonFileConnector for the simpler single-path case.
        var parts = connectionInfo.Split(new[] { "||" }, 2, StringSplitOptions.None);
        if (parts.Length != 2)
            throw new ArgumentException("connectionInfo must be \"<connectionString>||<sql>\" for SqlServerConnector.ReadRowsAsync.");

        return await ExecuteQueryAsync(parts[0], parts[1], ct);
    }

    private async Task<List<IReadOnlyDictionary<string, object?>>> ExecuteQueryAsync(string connectionString, string sql, CancellationToken ct)
    {
        var results = new List<IReadOnlyDictionary<string, object?>>();

        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);

        using var command = new SqlCommand(sql, connection) { CommandTimeout = 30 };
        using var reader = await command.ExecuteReaderAsync(ct);

        var columnNames = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();

        while (await reader.ReadAsync(ct))
        {
            var row = new Dictionary<string, object?>(columnNames.Length);
            for (var i = 0; i < columnNames.Length; i++)
                row[columnNames[i]] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            results.Add(row);
        }

        _logger.LogInformation("SQL Server query returned {Count} row(s).", results.Count);
        return results;
    }
}
