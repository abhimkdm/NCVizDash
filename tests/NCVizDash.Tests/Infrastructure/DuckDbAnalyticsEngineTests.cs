using Microsoft.Extensions.Logging.Abstractions;
using NCVizDash.DuckDB;
using NCVizDash.Models;
using Xunit;

namespace NCVizDash.Tests.Infrastructure;

/// <summary>
/// Integration tests for <see cref="DuckDbAnalyticsEngine"/>.
/// These exercise the real in-memory DuckDB engine and therefore require the
/// DuckDB native binaries to be present (restored automatically via the
/// DuckDB.NET.Bindings.Full NuGet package on a Windows build agent).
/// </summary>
public sealed class DuckDbAnalyticsEngineTests : IDisposable
{
    private readonly DuckDbAnalyticsEngine _sut = new(NullLogger<DuckDbAnalyticsEngine>.Instance);

    private static DataSourceDescriptor BuildDescriptor() => new()
    {
        Name = "Sales Data",
        SourceType = "ExcelTable",
        SheetName = "Sheet1",
        Fields =
        [
            new FieldDescriptor { Name = "Department", FieldType = FieldType.Dimension, ClrType = "System.String" },
            new FieldDescriptor { Name = "Revenue",     FieldType = FieldType.Measure,   ClrType = "System.Double" },
            new FieldDescriptor { Name = "OrderDate",   FieldType = FieldType.Time,      ClrType = "System.DateTime" },
            new FieldDescriptor { Name = "IsClosed",    FieldType = FieldType.Filter,    ClrType = "System.Boolean" }
        ]
    };

    private static List<IReadOnlyDictionary<string, object?>> BuildRows() =>
    [
        new Dictionary<string, object?>
        {
            ["Department"] = "Engineering", ["Revenue"] = 1000.0,
            ["OrderDate"] = new DateTime(2026, 1, 15), ["IsClosed"] = true
        },
        new Dictionary<string, object?>
        {
            ["Department"] = "Sales", ["Revenue"] = 2500.0,
            ["OrderDate"] = new DateTime(2026, 2, 10), ["IsClosed"] = false
        },
        new Dictionary<string, object?>
        {
            ["Department"] = "Engineering", ["Revenue"] = 750.0,
            ["OrderDate"] = new DateTime(2026, 3, 5), ["IsClosed"] = false
        }
    ];

    [Fact]
    public async Task LoadDataSourceAsync_ThenQuery_ReturnsAllRows()
    {
        var descriptor = BuildDescriptor();
        await _sut.LoadDataSourceAsync(descriptor, BuildRows());

        var tableName = _sut.GetTableName(descriptor.Id);
        Assert.NotNull(tableName);

        var results = await _sut.QueryAsync($"SELECT * FROM \"{tableName}\";");

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public async Task LoadDataSourceAsync_ThenGroupByQuery_AggregatesCorrectly()
    {
        var descriptor = BuildDescriptor();
        await _sut.LoadDataSourceAsync(descriptor, BuildRows());

        var tableName = _sut.GetTableName(descriptor.Id);
        var results = await _sut.QueryAsync(
            $"SELECT department, SUM(revenue) AS total FROM \"{tableName}\" GROUP BY department ORDER BY department;");

        Assert.Equal(2, results.Count);

        var engineering = results.First(r => r["department"]!.ToString() == "Engineering");
        Assert.Equal(1750.0, Convert.ToDouble(engineering["total"]));
    }

    [Fact]
    public async Task UnloadDataSourceAsync_RemovesTable()
    {
        var descriptor = BuildDescriptor();
        await _sut.LoadDataSourceAsync(descriptor, BuildRows());

        await _sut.UnloadDataSourceAsync(descriptor.Id);

        Assert.Null(_sut.GetTableName(descriptor.Id));
    }

    [Fact]
    public async Task LoadDataSourceAsync_ReloadingSameSource_ReplacesData()
    {
        var descriptor = BuildDescriptor();
        await _sut.LoadDataSourceAsync(descriptor, BuildRows());
        await _sut.LoadDataSourceAsync(descriptor, BuildRows().Take(1).ToList());

        var tableName = _sut.GetTableName(descriptor.Id);
        var results = await _sut.QueryAsync($"SELECT * FROM \"{tableName}\";");

        Assert.Single(results);
    }

    public void Dispose() => _sut.Dispose();
}
