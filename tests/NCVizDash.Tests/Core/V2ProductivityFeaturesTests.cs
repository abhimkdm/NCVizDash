using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NCVizDash.Core.Abstractions;
using NCVizDash.Models;
using NCVizDash.TaskPane.Generation;
using NCVizDash.TaskPane.Presentation;
using NCVizDash.TaskPane.Services;
using NCVizDash.TaskPane.Templates;
using Xunit;

namespace NCVizDash.Tests.Core;

/// <summary>Smoke tests for the v2.0 Productivity Features prompt (One-Click Generator, Templates, Story Mode, Live Refresh).</summary>
public sealed class V2ProductivityFeaturesTests
{
    private static DataSourceDescriptor MakeFullSource()
    {
        var ds = new DataSourceDescriptor { Name = "Sales" };
        ds.Fields.Add(new FieldDescriptor { Name = "Revenue", DisplayName = "Revenue", FieldType = FieldType.Measure });
        ds.Fields.Add(new FieldDescriptor { Name = "Cost", DisplayName = "Cost", FieldType = FieldType.Measure });
        ds.Fields.Add(new FieldDescriptor { Name = "Region", DisplayName = "Region", FieldType = FieldType.Dimension });
        ds.Fields.Add(new FieldDescriptor { Name = "Product", DisplayName = "Product", FieldType = FieldType.Dimension });
        ds.Fields.Add(new FieldDescriptor { Name = "OrderDate", DisplayName = "Order Date", FieldType = FieldType.Time });
        return ds;
    }

    // ── Feature 1: One-Click Dashboard Generator ─────────────────────────────

    [Fact]
    public void Generate_FullFieldSet_ProducesAllSectionTypes()
    {
        var sut = new OneClickDashboardGenerator(NullLogger<OneClickDashboardGenerator>.Instance);
        var dashboard = sut.Generate(MakeFullSource());

        var visualTypes = dashboard.Widgets.Select(w => w.VisualType).ToHashSet();

        Assert.Contains(VisualType.Kpi, visualTypes);
        Assert.Contains(VisualType.Line, visualTypes);   // trend (time field present)
        Assert.Contains(VisualType.Bar, visualTypes);    // category analysis + top/bottom N
        Assert.Contains(VisualType.Pie, visualTypes);
        Assert.Contains(VisualType.Table, visualTypes);  // summary
    }

    [Fact]
    public void Generate_TopAndBottomWidgets_HaveOppositeSortDirection()
    {
        var sut = new OneClickDashboardGenerator(NullLogger<OneClickDashboardGenerator>.Instance);
        var dashboard = sut.Generate(MakeFullSource());

        var top = dashboard.Widgets.Single(w => w.Title.StartsWith("Top 10"));
        var bottom = dashboard.Widgets.Single(w => w.Title.StartsWith("Bottom 10"));

        Assert.Equal(10, top.TopN);
        Assert.True(top.TopNDescending);
        Assert.Equal(10, bottom.TopN);
        Assert.False(bottom.TopNDescending);
    }

    [Fact]
    public void Generate_NoTimeField_OmitsTrendChart_DoesNotThrow()
    {
        var source = new DataSourceDescriptor { Name = "NoTimeData" };
        source.Fields.Add(new FieldDescriptor { Name = "Revenue", DisplayName = "Revenue", FieldType = FieldType.Measure });

        var sut = new OneClickDashboardGenerator(NullLogger<OneClickDashboardGenerator>.Instance);
        var dashboard = sut.Generate(source);

        Assert.DoesNotContain(dashboard.Widgets, w => w.VisualType == VisualType.Line);
    }

    [Fact]
    public void Generate_NoMeasures_ProducesEmptyButValidDashboard()
    {
        var source = new DataSourceDescriptor { Name = "Empty" };
        var sut = new OneClickDashboardGenerator(NullLogger<OneClickDashboardGenerator>.Instance);

        var exception = Record.Exception(() => sut.Generate(source));

        Assert.Null(exception);
    }

    [Fact]
    public void Generate_KpiCount_CappedAtFour()
    {
        var source = new DataSourceDescriptor { Name = "ManyMeasures" };
        for (var i = 0; i < 8; i++)
            source.Fields.Add(new FieldDescriptor { Name = $"M{i}", DisplayName = $"M{i}", FieldType = FieldType.Measure });

        var sut = new OneClickDashboardGenerator(NullLogger<OneClickDashboardGenerator>.Instance);
        var dashboard = sut.Generate(source);

        Assert.Equal(4, dashboard.Widgets.Count(w => w.VisualType == VisualType.Kpi));
    }

    // ── Feature 2: Templates (Delivery Dashboard addition) ───────────────────

    [Fact]
    public void TemplateRegistry_Has11Templates_IncludingDelivery()
    {
        Assert.Equal(11, TemplateRegistry.All.Count);
        Assert.Contains(TemplateRegistry.All, t => t.Name == "Delivery Dashboard");
    }

    [Fact]
    public void InstantiateWithReport_InsufficientFields_ReportsUnfilledSlots()
    {
        var source = new DataSourceDescriptor { Name = "Sparse" }; // no fields
        var template = TemplateRegistry.All.First(t => t.Name == "Delivery Dashboard");
        var sut = new TemplateInstantiationService(NullLogger<TemplateInstantiationService>.Instance);

        var result = sut.InstantiateWithReport(template, source);

        Assert.False(result.IsComplete);
        Assert.Equal(template.Slots.Count, result.UnfilledSlots.Count);
    }

    [Fact]
    public void InstantiateWithReport_FullFieldSet_IsComplete()
    {
        var source = MakeFullSource();
        var template = TemplateRegistry.All.First(t => t.Name == "Sales Dashboard");
        var sut = new TemplateInstantiationService(NullLogger<TemplateInstantiationService>.Instance);

        var result = sut.InstantiateWithReport(template, source);

        Assert.True(result.IsComplete);
    }

    // ── Feature 3: Story Mode ─────────────────────────────────────────────────

    [Fact]
    public void PresentationController_Start_WithBookmarks_LoadsPagesAndActivates()
    {
        var globalFilterManager = new GlobalFilterManager(NullLogger<GlobalFilterManager>.Instance);
        var bookmarkManager = new BookmarkManager(NullLogger<BookmarkManager>.Instance, globalFilterManager);
        var dashboard = new Dashboard();
        globalFilterManager.SetDashboard(dashboard);

        bookmarkManager.Capture(dashboard.Id, "Page 1");
        bookmarkManager.Capture(dashboard.Id, "Page 2");

        var sut = new PresentationController(NullLogger<PresentationController>.Instance, bookmarkManager);
        sut.Start(dashboard.Id);

        Assert.True(sut.IsActive);
        Assert.Equal(2, sut.Pages.Count);
        Assert.Equal(0, sut.CurrentPageIndex);
    }

    [Fact]
    public void PresentationController_Next_WrapsAroundToFirstPage()
    {
        var globalFilterManager = new GlobalFilterManager(NullLogger<GlobalFilterManager>.Instance);
        var bookmarkManager = new BookmarkManager(NullLogger<BookmarkManager>.Instance, globalFilterManager);
        var dashboard = new Dashboard();
        globalFilterManager.SetDashboard(dashboard);
        bookmarkManager.Capture(dashboard.Id, "P1");
        bookmarkManager.Capture(dashboard.Id, "P2");

        var sut = new PresentationController(NullLogger<PresentationController>.Instance, bookmarkManager);
        sut.Start(dashboard.Id);

        sut.Next(); // → page 2
        sut.Next(); // wraps → page 1

        Assert.Equal(0, sut.CurrentPageIndex);
    }

    [Fact]
    public void PresentationController_Previous_WrapsAroundToLastPage()
    {
        var globalFilterManager = new GlobalFilterManager(NullLogger<GlobalFilterManager>.Instance);
        var bookmarkManager = new BookmarkManager(NullLogger<BookmarkManager>.Instance, globalFilterManager);
        var dashboard = new Dashboard();
        globalFilterManager.SetDashboard(dashboard);
        bookmarkManager.Capture(dashboard.Id, "P1");
        bookmarkManager.Capture(dashboard.Id, "P2");

        var sut = new PresentationController(NullLogger<PresentationController>.Instance, bookmarkManager);
        sut.Start(dashboard.Id);

        sut.Previous(); // wraps → last page

        Assert.Equal(1, sut.CurrentPageIndex);
    }

    [Fact]
    public void PresentationController_Stop_DeactivatesAndStopsAutoPlay()
    {
        var globalFilterManager = new GlobalFilterManager(NullLogger<GlobalFilterManager>.Instance);
        var bookmarkManager = new BookmarkManager(NullLogger<BookmarkManager>.Instance, globalFilterManager);
        var sut = new PresentationController(NullLogger<PresentationController>.Instance, bookmarkManager);

        sut.Start(Guid.NewGuid());
        sut.Stop();

        Assert.False(sut.IsActive);
        Assert.False(sut.IsPlaying);
    }

    [Fact]
    public void PresentationController_NoBookmarks_StartsInactivePageList()
    {
        var globalFilterManager = new GlobalFilterManager(NullLogger<GlobalFilterManager>.Instance);
        var bookmarkManager = new BookmarkManager(NullLogger<BookmarkManager>.Instance, globalFilterManager);
        var sut = new PresentationController(NullLogger<PresentationController>.Instance, bookmarkManager);

        sut.Start(Guid.NewGuid());

        Assert.Empty(sut.Pages);
        Assert.Null(sut.CurrentPage);
    }

    // ── Feature 4: Live Refresh ────────────────────────────────────────────────

    [Fact]
    public async Task ExplorerPanelViewModel_RefreshSheetAsync_OnlyRefreshesMatchingSheet()
    {
        var (reader, engine) = MakeMocks();
        var sheetASource = MakeSourceOnSheet("TableA", "Sheet1");
        var sheetBSource = MakeSourceOnSheet("TableB", "Sheet2");

        reader.Setup(r => r.GetDataSourcesAsync(It.IsAny<CancellationToken>()))
              .ReturnsAsync(new[] { sheetASource, sheetBSource });
        reader.Setup(r => r.ReadRowsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(Array.Empty<IReadOnlyDictionary<string, object?>>());

        var sut = new ExplorerPanelViewModel(NullLogger<ExplorerPanelViewModel>.Instance, reader.Object, engine.Object);
        await sut.LoadDataSourcesAsync();

        var refreshedIds = await sut.RefreshSheetAsync("Sheet1");

        Assert.Single(refreshedIds);
        Assert.Equal(sheetASource.Id, refreshedIds[0]);
    }

    [Fact]
    public async Task ExplorerPanelViewModel_RefreshSheetAsync_NoMatch_ReturnsEmpty()
    {
        var (reader, engine) = MakeMocks();
        var sut = new ExplorerPanelViewModel(NullLogger<ExplorerPanelViewModel>.Instance, reader.Object, engine.Object);

        var refreshedIds = await sut.RefreshSheetAsync("NonExistentSheet");

        Assert.Empty(refreshedIds);
    }

    [Fact]
    public void CanvasPanelViewModel_NotifyDataSourceRefreshed_RaisesEvent()
    {
        var sut = TestFactories.MakeCanvasPanelViewModel();
        Guid? received = null;
        sut.DataSourceRefreshed += (_, id) => received = id;

        var testId = Guid.NewGuid();
        sut.NotifyDataSourceRefreshed(testId);

        Assert.Equal(testId, received);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (Mock<IExcelDataReader> reader, Mock<IAnalyticsEngine> engine) MakeMocks() =>
        (new Mock<IExcelDataReader>(), TestFactories.MakeAnalyticsEngineMock());

    private static DataSourceDescriptor MakeSourceOnSheet(string name, string sheetName) =>
        new() { Name = name, SourceType = "ExcelTable", SheetName = sheetName };
}
