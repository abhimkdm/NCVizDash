using NCVizDash.Models;

namespace NCVizDash.Core.Abstractions;

/// <summary>
/// A pluggable external data source (Phase 14): CSV, JSON, SQL Server, REST API,
/// SharePoint, etc. Produces the exact same shape <see cref="IExcelDataReader"/>
/// does — a <see cref="DataSourceDescriptor"/> plus row dictionaries — so every
/// downstream consumer (rule engine, chart engine, DuckDB, filters) needs zero
/// changes to work with connector-sourced data instead of Excel-sourced data.
/// </summary>
public interface IDataConnector
{
    /// <summary>Short, stable identifier for this connector type (e.g. "csv", "sqlserver", "rest").</summary>
    string ConnectorType { get; }

    /// <summary>
    /// Connects to the source described by <paramref name="connectionInfo"/> (a
    /// connector-specific string — a file path, connection string, or URL) and
    /// returns its discoverable data source(s).
    /// </summary>
    Task<IReadOnlyList<DataSourceDescriptor>> DiscoverAsync(string connectionInfo, CancellationToken ct = default);

    /// <summary>Reads all rows for a previously-discovered data source.</summary>
    Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ReadRowsAsync(
        DataSourceDescriptor descriptor, string connectionInfo, CancellationToken ct = default);
}
