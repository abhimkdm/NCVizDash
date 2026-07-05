using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NCVizDash.Core.Abstractions;
using NCVizDash.Core.Analytics;
using NCVizDash.TaskPane.Services;
using Xunit;

namespace NCVizDash.Tests.Core;

/// <summary>
/// Unit tests for <see cref="DistinctValueService"/>. Deliberately exercises
/// arbitrary, made-up field and table names (not a fixed business domain) to
/// prove the service works generically for any kind of data.
/// </summary>
public sealed class DistinctValueServiceTests
{
    private static (Mock<IAnalyticsEngine> engine, DistinctValueService sut) MakeSut()
    {
        var engine = new Mock<IAnalyticsEngine>();
        var sut = new DistinctValueService(engine.Object, NullLogger<DistinctValueService>.Instance);
        return (engine, sut);
    }

    [Fact]
    public async Task GetDistinctValuesAsync_NoTableLoaded_ReturnsEmptyList()
    {
        var (engine, sut) = MakeSut();
        engine.Setup(e => e.GetTableName(It.IsAny<Guid>())).Returns((string?)null);

        var result = await sut.GetDistinctValuesAsync(Guid.NewGuid(), "AnyField");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetDistinctValuesAsync_EmptyFieldName_ReturnsEmptyList()
    {
        var (engine, sut) = MakeSut();
        engine.Setup(e => e.GetTableName(It.IsAny<Guid>())).Returns("some_table");

        var result = await sut.GetDistinctValuesAsync(Guid.NewGuid(), "");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetDistinctValuesAsync_ArbitraryField_ReturnsSortedDistinctValues()
    {
        var (engine, sut) = MakeSut();
        engine.Setup(e => e.GetTableName(It.IsAny<Guid>())).Returns("widgets_inventory");
        engine.Setup(e => e.QueryAsync(It.IsAny<QuerySpec>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(new List<IReadOnlyDictionary<string, object?>>
              {
                  new Dictionary<string, object?> { ["warehouse_zone"] = "Zulu" },
                  new Dictionary<string, object?> { ["warehouse_zone"] = "Alpha" },
                  new Dictionary<string, object?> { ["warehouse_zone"] = "Mike" }
              });

        var result = await sut.GetDistinctValuesAsync(Guid.NewGuid(), "Warehouse Zone");

        Assert.Equal(["Alpha", "Mike", "Zulu"], result);
    }

    [Fact]
    public async Task GetDistinctValuesAsync_SendsGroupByQuerySpec_ForRequestedField()
    {
        var (engine, sut) = MakeSut();
        QuerySpec? captured = null;

        engine.Setup(e => e.GetTableName(It.IsAny<Guid>())).Returns("t1");
        engine.Setup(e => e.QueryAsync(It.IsAny<QuerySpec>(), It.IsAny<CancellationToken>()))
              .Callback<QuerySpec, CancellationToken>((spec, _) => captured = spec)
              .ReturnsAsync(new List<IReadOnlyDictionary<string, object?>>());

        await sut.GetDistinctValuesAsync(Guid.NewGuid(), "Any Custom Field");

        Assert.NotNull(captured);
        Assert.Contains("Any Custom Field", captured!.Dimensions);
        Assert.Empty(captured.Measures); // no aggregation — just distinct grouping
    }

    [Fact]
    public async Task GetDistinctValuesAsync_ExcludesNullAndEmptyValues()
    {
        var (engine, sut) = MakeSut();
        engine.Setup(e => e.GetTableName(It.IsAny<Guid>())).Returns("t1");
        engine.Setup(e => e.QueryAsync(It.IsAny<QuerySpec>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(new List<IReadOnlyDictionary<string, object?>>
              {
                  new Dictionary<string, object?> { ["status"] = "Active" },
                  new Dictionary<string, object?> { ["status"] = null },
                  new Dictionary<string, object?> { ["status"] = "" }
              });

        var result = await sut.GetDistinctValuesAsync(Guid.NewGuid(), "status");

        Assert.Single(result);
        Assert.Equal("Active", result[0]);
    }

    [Fact]
    public async Task GetDistinctValuesAsync_DeduplicatesCaseInsensitively()
    {
        var (engine, sut) = MakeSut();
        engine.Setup(e => e.GetTableName(It.IsAny<Guid>())).Returns("t1");
        engine.Setup(e => e.QueryAsync(It.IsAny<QuerySpec>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(new List<IReadOnlyDictionary<string, object?>>
              {
                  new Dictionary<string, object?> { ["tag"] = "urgent" },
                  new Dictionary<string, object?> { ["tag"] = "Urgent" }
              });

        var result = await sut.GetDistinctValuesAsync(Guid.NewGuid(), "tag");

        Assert.Single(result);
    }

    [Fact]
    public async Task GetDistinctValuesAsync_QueryThrows_ReturnsEmptyList_DoesNotPropagate()
    {
        var (engine, sut) = MakeSut();
        engine.Setup(e => e.GetTableName(It.IsAny<Guid>())).Returns("t1");
        engine.Setup(e => e.QueryAsync(It.IsAny<QuerySpec>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new InvalidOperationException("boom"));

        var result = await sut.GetDistinctValuesAsync(Guid.NewGuid(), "field");

        Assert.Empty(result);
    }
}
