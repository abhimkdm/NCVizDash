using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NCVizDash.ChartEngine;
using NCVizDash.Core.Abstractions;
using NCVizDash.Core.Analytics;
using NCVizDash.Models;
using NCVizDash.TaskPane.Services;
using System.Text.Json;
using Xunit;

namespace NCVizDash.Tests.Core;

/// <summary>Unit tests for <see cref="WidgetRenderCoordinator"/>.</summary>
public sealed class WidgetRenderCoordinatorTests
{
    private static (Mock<IAnalyticsEngine> engine, Mock<IFilterManager> filterManager,
        Mock<IGlobalFilterManager> globalFilterManager, WidgetRenderCoordinator sut) MakeSut()
    {
        var engine = new Mock<IAnalyticsEngine>();
        var filterManager = new Mock<IFilterManager>();
        filterManager.Setup(f => f.GetActiveFilters(It.IsAny<Guid?>())).Returns(new List<WidgetFilter>());

        var globalFilterManager = new Mock<IGlobalFilterManager>();
        globalFilterManager.Setup(f => f.GetEnabledFilters()).Returns(new List<WidgetFilter>());

        var chartEngine = new EChartsChartEngine(NullLogger<EChartsChartEngine>.Instance);
        var sut = new WidgetRenderCoordinator(
            engine.Object, chartEngine, filterManager.Object, globalFilterManager.Object,
            NullLogger<WidgetRenderCoordinator>.Instance);

        return (engine, filterManager, globalFilterManager, sut);
    }

    private static DashboardWidget MakeWidget(VisualType type = VisualType.Bar)
    {
        var w = new DashboardWidget { Title = "Test", VisualType = type, DataSourceId = Guid.NewGuid() };
        w.DimensionFields.Add("Department");
        w.MeasureFields.Add("Revenue");
        return w;
    }

    [Fact]
    public async Task RenderWidgetAsync_NoTableLoaded_ReturnsErrorEnvelope()
    {
        var (engine, _, _, sut) = MakeSut();
        engine.Setup(e => e.GetTableName(It.IsAny<Guid>())).Returns((string?)null);

        var result = await sut.RenderWidgetAsync(MakeWidget(), "Light");

        using var doc = JsonDocument.Parse(result);
        Assert.Equal("error", doc.RootElement.GetProperty("kind").GetString());
    }

    [Fact]
    public async Task RenderWidgetAsync_NoFieldsConfigured_ReturnsErrorEnvelope()
    {
        var (engine, _, _, sut) = MakeSut();
        engine.Setup(e => e.GetTableName(It.IsAny<Guid>())).Returns("sales_abc123");

        var widget = new DashboardWidget { Title = "Empty", VisualType = VisualType.Bar, DataSourceId = Guid.NewGuid() };

        var result = await sut.RenderWidgetAsync(widget, "Light");

        using var doc = JsonDocument.Parse(result);
        Assert.Equal("error", doc.RootElement.GetProperty("kind").GetString());
    }

    [Fact]
    public async Task RenderWidgetAsync_ValidWidget_QueriesAndReturnsEchartsEnvelope()
    {
        var (engine, _, _, sut) = MakeSut();
        engine.Setup(e => e.GetTableName(It.IsAny<Guid>())).Returns("sales_abc123");
        engine.Setup(e => e.QueryAsync(It.IsAny<QuerySpec>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(new List<IReadOnlyDictionary<string, object?>>
              {
                  new Dictionary<string, object?> { ["department"] = "Eng", ["revenue"] = 100.0 }
              });

        var result = await sut.RenderWidgetAsync(MakeWidget(), "Light");

        using var doc = JsonDocument.Parse(result);
        Assert.Equal("echarts", doc.RootElement.GetProperty("kind").GetString());
    }

    [Fact]
    public async Task RenderWidgetAsync_AggregateVisualType_BuildsSumMeasureSpec()
    {
        var (engine, _, _, sut) = MakeSut();
        QuerySpec? captured = null;

        engine.Setup(e => e.GetTableName(It.IsAny<Guid>())).Returns("sales_abc123");
        engine.Setup(e => e.QueryAsync(It.IsAny<QuerySpec>(), It.IsAny<CancellationToken>()))
              .Callback<QuerySpec, CancellationToken>((spec, _) => captured = spec)
              .ReturnsAsync(new List<IReadOnlyDictionary<string, object?>>());

        await sut.RenderWidgetAsync(MakeWidget(VisualType.Bar), "Light");

        Assert.NotNull(captured);
        Assert.Equal("sales_abc123", captured!.TableName);
        Assert.Single(captured.Measures);
        Assert.Equal(AggregateFunction.Sum, captured.Measures[0].Aggregate);
    }

    [Fact]
    public async Task RenderWidgetAsync_ScatterVisualType_RequestsRawRows()
    {
        var (engine, _, _, sut) = MakeSut();
        QuerySpec? captured = null;

        engine.Setup(e => e.GetTableName(It.IsAny<Guid>())).Returns("t1");
        engine.Setup(e => e.QueryAsync(It.IsAny<QuerySpec>(), It.IsAny<CancellationToken>()))
              .Callback<QuerySpec, CancellationToken>((spec, _) => captured = spec)
              .ReturnsAsync(new List<IReadOnlyDictionary<string, object?>>());

        var widget = new DashboardWidget { Title = "Scatter", VisualType = VisualType.Scatter, DataSourceId = Guid.NewGuid() };
        widget.MeasureFields.AddRange(["X", "Y"]);

        await sut.RenderWidgetAsync(widget, "Light");

        Assert.NotNull(captured);
        Assert.All(captured!.Measures, m => Assert.Equal(AggregateFunction.None, m.Aggregate));
    }

    [Fact]
    public async Task RenderWidgetAsync_QueryThrows_ReturnsErrorEnvelope_DoesNotPropagate()
    {
        var (engine, _, _, sut) = MakeSut();
        engine.Setup(e => e.GetTableName(It.IsAny<Guid>())).Returns("t1");
        engine.Setup(e => e.QueryAsync(It.IsAny<QuerySpec>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new InvalidOperationException("boom"));

        var result = await sut.RenderWidgetAsync(MakeWidget(), "Light");

        using var doc = JsonDocument.Parse(result);
        Assert.Equal("error", doc.RootElement.GetProperty("kind").GetString());
    }

    [Fact]
    public async Task RenderWidgetAsync_OnlyEnabledLocalFilters_ArePassedToQuerySpec()
    {
        var (engine, _, _, sut) = MakeSut();
        QuerySpec? captured = null;

        engine.Setup(e => e.GetTableName(It.IsAny<Guid>())).Returns("t1");
        engine.Setup(e => e.QueryAsync(It.IsAny<QuerySpec>(), It.IsAny<CancellationToken>()))
              .Callback<QuerySpec, CancellationToken>((spec, _) => captured = spec)
              .ReturnsAsync(new List<IReadOnlyDictionary<string, object?>>());

        var widget = MakeWidget();
        widget.LocalFilters.Add(new WidgetFilter { FieldName = "Quarter", Operator = FilterOperator.NotIn, Values = ["Q1"], IsEnabled = true });
        widget.LocalFilters.Add(new WidgetFilter { FieldName = "Region", Operator = FilterOperator.Equals, Values = ["EMEA"], IsEnabled = false });

        await sut.RenderWidgetAsync(widget, "Light");

        Assert.NotNull(captured);
        Assert.Contains(captured!.Filters, f => f.FieldName == "Quarter");
        Assert.DoesNotContain(captured.Filters, f => f.FieldName == "Region");
    }

    [Fact]
    public async Task RenderWidgetAsync_TableVisualType_UsesSmallerRowLimit()
    {
        var (engine, _, _, sut) = MakeSut();
        QuerySpec? captured = null;

        engine.Setup(e => e.GetTableName(It.IsAny<Guid>())).Returns("t1");
        engine.Setup(e => e.QueryAsync(It.IsAny<QuerySpec>(), It.IsAny<CancellationToken>()))
              .Callback<QuerySpec, CancellationToken>((spec, _) => captured = spec)
              .ReturnsAsync(new List<IReadOnlyDictionary<string, object?>>());

        await sut.RenderWidgetAsync(MakeWidget(VisualType.Table), "Light");

        Assert.Equal(200, captured!.Limit);
    }

    [Fact]
    public async Task RenderWidgetAsync_NonTableVisualType_UsesDefaultRowLimit()
    {
        var (engine, _, _, sut) = MakeSut();
        QuerySpec? captured = null;

        engine.Setup(e => e.GetTableName(It.IsAny<Guid>())).Returns("t1");
        engine.Setup(e => e.QueryAsync(It.IsAny<QuerySpec>(), It.IsAny<CancellationToken>()))
              .Callback<QuerySpec, CancellationToken>((spec, _) => captured = spec)
              .ReturnsAsync(new List<IReadOnlyDictionary<string, object?>>());

        await sut.RenderWidgetAsync(MakeWidget(VisualType.Bar), "Light");

        Assert.Equal(500, captured!.Limit);
    }

    // ── Phase 8: cross-filter merging ────────────────────────────────────────

    [Fact]
    public async Task RenderWidgetAsync_CrossFilterTarget_MergesActiveCrossFilters()
    {
        var (engine, _, globalFilterManager, sut) = MakeSut();
        var filterManagerMock = new Mock<IFilterManager>();
        var widget = MakeWidget();

        // Rebuild sut with a filter manager we can assert against for this specific widget id.
        engine.Setup(e => e.GetTableName(It.IsAny<Guid>())).Returns("t1");
        QuerySpec? captured = null;
        engine.Setup(e => e.QueryAsync(It.IsAny<QuerySpec>(), It.IsAny<CancellationToken>()))
              .Callback<QuerySpec, CancellationToken>((spec, _) => captured = spec)
              .ReturnsAsync(new List<IReadOnlyDictionary<string, object?>>());

        filterManagerMock.Setup(f => f.GetActiveFilters(widget.Id))
                         .Returns(new List<WidgetFilter> { new() { FieldName = "Region", Operator = FilterOperator.Equals, Values = ["EMEA"] } });

        var chartEngine = new EChartsChartEngine(NullLogger<EChartsChartEngine>.Instance);
        var localSut = new WidgetRenderCoordinator(
            engine.Object, chartEngine, filterManagerMock.Object, globalFilterManager.Object,
            NullLogger<WidgetRenderCoordinator>.Instance);

        await localSut.RenderWidgetAsync(widget, "Light");

        Assert.Contains(captured!.Filters, f => f.FieldName == "Region");
    }

    [Fact]
    public async Task RenderWidgetAsync_NotCrossFilterTarget_IgnoresActiveCrossFilters()
    {
        var (engine, filterManager, _, sut) = MakeSut();
        QuerySpec? captured = null;
        var widget = MakeWidget();
        widget.IsCrossFilterTarget = false;

        engine.Setup(e => e.GetTableName(It.IsAny<Guid>())).Returns("t1");
        engine.Setup(e => e.QueryAsync(It.IsAny<QuerySpec>(), It.IsAny<CancellationToken>()))
              .Callback<QuerySpec, CancellationToken>((spec, _) => captured = spec)
              .ReturnsAsync(new List<IReadOnlyDictionary<string, object?>>());

        await sut.RenderWidgetAsync(widget, "Light");

        filterManager.Verify(f => f.GetActiveFilters(It.IsAny<Guid?>()), Times.Never);
        Assert.Empty(captured!.Filters);
    }

    [Fact]
    public async Task RenderWidgetAsync_ExcludesOwnFiltersFromCrossFilterQuery()
    {
        var (engine, filterManager, _, sut) = MakeSut();
        var widget = MakeWidget();

        engine.Setup(e => e.GetTableName(It.IsAny<Guid>())).Returns("t1");
        engine.Setup(e => e.QueryAsync(It.IsAny<QuerySpec>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(new List<IReadOnlyDictionary<string, object?>>());

        await sut.RenderWidgetAsync(widget, "Light");

        filterManager.Verify(f => f.GetActiveFilters(widget.Id), Times.Once);
    }

    // ── Phase 9: global-filter merging ───────────────────────────────────────

    [Fact]
    public async Task RenderWidgetAsync_MergesEnabledGlobalFilters_Unconditionally()
    {
        var (engine, _, globalFilterManager, sut) = MakeSut();
        QuerySpec? captured = null;

        engine.Setup(e => e.GetTableName(It.IsAny<Guid>())).Returns("t1");
        engine.Setup(e => e.QueryAsync(It.IsAny<QuerySpec>(), It.IsAny<CancellationToken>()))
              .Callback<QuerySpec, CancellationToken>((spec, _) => captured = spec)
              .ReturnsAsync(new List<IReadOnlyDictionary<string, object?>>());

        globalFilterManager.Setup(f => f.GetEnabledFilters())
            .Returns(new List<WidgetFilter> { new() { FieldName = "FiscalYear", Operator = FilterOperator.Equals, Values = ["2026"] } });

        await sut.RenderWidgetAsync(MakeWidget(), "Light");

        Assert.Contains(captured!.Filters, f => f.FieldName == "FiscalYear");
    }

    [Fact]
    public async Task RenderWidgetAsync_GlobalFilters_ApplyEvenWhenNotCrossFilterTarget()
    {
        var (engine, _, globalFilterManager, sut) = MakeSut();
        QuerySpec? captured = null;
        var widget = MakeWidget();
        widget.IsCrossFilterTarget = false; // opted out of cross-filtering, but NOT global filtering

        engine.Setup(e => e.GetTableName(It.IsAny<Guid>())).Returns("t1");
        engine.Setup(e => e.QueryAsync(It.IsAny<QuerySpec>(), It.IsAny<CancellationToken>()))
              .Callback<QuerySpec, CancellationToken>((spec, _) => captured = spec)
              .ReturnsAsync(new List<IReadOnlyDictionary<string, object?>>());

        globalFilterManager.Setup(f => f.GetEnabledFilters())
            .Returns(new List<WidgetFilter> { new() { FieldName = "FiscalYear", Operator = FilterOperator.Equals, Values = ["2026"] } });

        await sut.RenderWidgetAsync(widget, "Light");

        Assert.Contains(captured!.Filters, f => f.FieldName == "FiscalYear");
    }

    [Fact]
    public async Task RenderWidgetAsync_NoGlobalFilters_DoesNotAddAnyFilter()
    {
        var (engine, _, globalFilterManager, sut) = MakeSut();
        QuerySpec? captured = null;

        engine.Setup(e => e.GetTableName(It.IsAny<Guid>())).Returns("t1");
        engine.Setup(e => e.QueryAsync(It.IsAny<QuerySpec>(), It.IsAny<CancellationToken>()))
              .Callback<QuerySpec, CancellationToken>((spec, _) => captured = spec)
              .ReturnsAsync(new List<IReadOnlyDictionary<string, object?>>());

        await sut.RenderWidgetAsync(MakeWidget(), "Light");

        Assert.Empty(captured!.Filters);
    }
}
