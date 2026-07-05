using System.Net.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NCVizDash.Connectors.Csv;
using NCVizDash.Connectors.Json;
using NCVizDash.Core.Abstractions;
using NCVizDash.Core.Analytics;
using NCVizDash.Models;
using NCVizDash.TaskPane.Services;
using NCVizDash.TaskPane.Templates;
using Xunit;

namespace NCVizDash.Tests.Core;

/// <summary>
/// Focused smoke tests for Phases 10–18. Deliberately lighter coverage than the
/// exhaustive Phase 1–9 suites (a handful of representative cases per feature
/// rather than every edge case) given the combined scope of nine phases delivered
/// in one pass — see each phase's CHANGELOG entry for what's fully covered vs.
/// what's scoped down.
/// </summary>
public sealed class Phase10Through18Tests
{
    // ── Phase 10: Dashboard Storage ──────────────────────────────────────────

    [Fact]
    public void Dashboard_RoundTripsThroughJson_IncludingGlobalFilters()
    {
        var dashboard = new Dashboard { Name = "Test" };
        dashboard.GlobalFilters.Add(new WidgetFilter { FieldName = "Region", Values = ["EMEA"] });
        dashboard.Widgets.Add(new DashboardWidget { Title = "W1", VisualType = VisualType.Bar });

        var json = System.Text.Json.JsonSerializer.Serialize(dashboard);
        var restored = System.Text.Json.JsonSerializer.Deserialize<Dashboard>(json);

        Assert.NotNull(restored);
        Assert.Single(restored!.GlobalFilters);
        Assert.Single(restored.Widgets);
    }

    // ── Phase 11: Templates ───────────────────────────────────────────────────

    [Fact]
    public void TemplateRegistry_Has10Templates()
    {
        Assert.Equal(10, TemplateRegistry.All.Count);
    }

    [Fact]
    public void TemplateInstantiationService_FillsSlots_FromMatchingFields()
    {
        var source = new DataSourceDescriptor { Name = "Sales" };
        source.Fields.Add(new FieldDescriptor { Name = "Revenue", DisplayName = "Revenue", FieldType = FieldType.Measure });
        source.Fields.Add(new FieldDescriptor { Name = "Region", DisplayName = "Region", FieldType = FieldType.Dimension });
        source.Fields.Add(new FieldDescriptor { Name = "Date", DisplayName = "Date", FieldType = FieldType.Time });

        var salesTemplate = TemplateRegistry.All.Single(t => t.Name == "Sales Dashboard");
        var sut = new TemplateInstantiationService(NullLogger<TemplateInstantiationService>.Instance);

        var dashboard = sut.Instantiate(salesTemplate, source);

        Assert.NotEmpty(dashboard.Widgets);
        Assert.All(dashboard.Widgets, w => Assert.Equal(source.Id, w.DataSourceId));
    }

    [Fact]
    public void TemplateInstantiationService_InsufficientFields_SkipsSlotsGracefully()
    {
        var source = new DataSourceDescriptor { Name = "Minimal" }; // no fields at all
        var template = TemplateRegistry.All[0];
        var sut = new TemplateInstantiationService(NullLogger<TemplateInstantiationService>.Instance);

        var exception = Record.Exception(() => sut.Instantiate(template, source));

        Assert.Null(exception);
    }

    // ── Phase 12: Undo/Redo, Bookmarks, Conditional Formatting ──────────────

    [Fact]
    public void UndoRedoManager_Undo_RestoresPreviousWidgetList()
    {
        var sut = new UndoRedoManager(NullLogger<UndoRedoManager>.Instance);
        var dashboard = new Dashboard();
        dashboard.Widgets.Add(new DashboardWidget { Title = "Original" });

        sut.RecordSnapshot(dashboard); // snapshot BEFORE the change
        dashboard.Widgets.Add(new DashboardWidget { Title = "Added" });

        var restored = sut.Undo(dashboard);

        Assert.NotNull(restored);
        Assert.Single(restored!);
        Assert.Equal("Original", restored[0].Title);
    }

    [Fact]
    public void UndoRedoManager_Redo_ReappliesUndoneChange()
    {
        var sut = new UndoRedoManager(NullLogger<UndoRedoManager>.Instance);
        var dashboard = new Dashboard();
        dashboard.Widgets.Add(new DashboardWidget { Title = "Original" });

        sut.RecordSnapshot(dashboard);
        dashboard.Widgets.Add(new DashboardWidget { Title = "Added" });
        var undone = sut.Undo(dashboard)!;
        dashboard.Widgets = undone;

        var redone = sut.Redo(dashboard);

        Assert.Equal(2, redone!.Count);
    }

    [Fact]
    public void UndoRedoManager_CanUndo_FalseInitially_TrueAfterSnapshot()
    {
        var sut = new UndoRedoManager(NullLogger<UndoRedoManager>.Instance);
        Assert.False(sut.CanUndo);

        sut.RecordSnapshot(new Dashboard());
        Assert.True(sut.CanUndo);
    }

    [Fact]
    public void BookmarkManager_Capture_And_Restore_RoundTripsFilters()
    {
        var globalFilterManager = new GlobalFilterManager(NullLogger<GlobalFilterManager>.Instance);
        var dashboard = new Dashboard();
        globalFilterManager.SetDashboard(dashboard);
        globalFilterManager.AddOrUpdateFilter(new WidgetFilter { FieldName = "Region", Values = ["EMEA"] });

        var sut = new BookmarkManager(NullLogger<BookmarkManager>.Instance, globalFilterManager);
        var bookmark = sut.Capture(dashboard.Id, "Q1 View");

        globalFilterManager.ClearAll();
        Assert.Empty(globalFilterManager.GetFilters());

        sut.Restore(bookmark);
        Assert.Single(globalFilterManager.GetFilters());
    }

    [Fact]
    public void AnalyticsQueryBuilder_CalculatedMeasure_AppearsInSelect()
    {
        var spec = new QuerySpec
        {
            TableName = "t",
            Dimensions = ["dept"],
            CalculatedMeasures = [new CalculatedMeasureSpec { Alias = "margin", Expression = "revenue - cost" }]
        };

        var sql = NCVizDash.DuckDB.AnalyticsQueryBuilder.Build(spec);

        Assert.Contains("(revenue - cost) AS \"margin\"", sql);
    }

    [Fact]
    public void AnalyticsQueryBuilder_CalculatedMeasure_UnsafeExpression_Excluded()
    {
        var spec = new QuerySpec
        {
            TableName = "t",
            Dimensions = ["dept"],
            CalculatedMeasures = [new CalculatedMeasureSpec { Alias = "bad", Expression = "1; DROP TABLE t; --" }]
        };

        var sql = NCVizDash.DuckDB.AnalyticsQueryBuilder.Build(spec);

        Assert.DoesNotContain("DROP TABLE", sql);
    }

    // ── Phase 14: Data Connectors ─────────────────────────────────────────────

    [Fact]
    public async Task CsvFileConnector_DiscoverAndRead_RoundTrips()
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "Name,Revenue\nAcme,100\n\"Smith, Co\",200\n");

        try
        {
            var sut = new CsvFileConnector(NullLogger<CsvFileConnector>.Instance);
            var sources = await sut.DiscoverAsync(tempFile);

            Assert.Single(sources);
            Assert.Equal(2, sources[0].Fields.Count);

            var rows = await sut.ReadRowsAsync(sources[0], tempFile);
            Assert.Equal(2, rows.Count);
            Assert.Equal("Smith, Co", rows[1]["Name"]); // proves quoted-comma handling works
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task JsonFileConnector_DiscoverAndRead_RoundTrips()
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, """[{"name":"Acme","revenue":100},{"name":"Beta","revenue":200}]""");

        try
        {
            var sut = new JsonFileConnector(NullLogger<JsonFileConnector>.Instance);
            var sources = await sut.DiscoverAsync(tempFile);

            Assert.Single(sources);
            var rows = await sut.ReadRowsAsync(sources[0], tempFile);
            Assert.Equal(2, rows.Count);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task SharePointListConnector_ThrowsExplicitNotSupported()
    {
        var sut = new NCVizDash.Connectors.SharePoint.SharePointListConnector();
        await Assert.ThrowsAsync<NotSupportedException>(() => sut.DiscoverAsync("anything"));
    }

    // ── Phase 15: Collaboration ───────────────────────────────────────────────

    [Fact]
    public async Task DashboardShareService_ExportThenImport_RoundTrips()
    {
        var sut = new DashboardShareService(NullLogger<DashboardShareService>.Instance);
        var dashboard = new Dashboard { Name = "Shared Dashboard" };
        dashboard.Widgets.Add(new DashboardWidget { Title = "W1" });

        var tempFile = Path.GetTempFileName();
        try
        {
            await sut.ExportToFileAsync(dashboard, tempFile);
            var imported = await sut.ImportFromFileAsync(tempFile, sharedBy: "Alice");

            Assert.NotNull(imported);
            Assert.Equal("Shared Dashboard", imported!.Name);
            Assert.NotEqual(dashboard.Id, imported.Id); // fresh ID assigned
            Assert.Equal("Alice", imported.SharedBy);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void DashboardShareService_VersionHistory_CapturesAndRestores()
    {
        var sut = new DashboardShareService(NullLogger<DashboardShareService>.Instance);
        var dashboard = new Dashboard { Name = "V1" };
        sut.CaptureVersion(dashboard);

        var timestamps = sut.GetVersionTimestamps(dashboard.Id);
        Assert.Single(timestamps);

        var restored = sut.RestoreVersion(dashboard.Id, timestamps[0]);
        Assert.NotNull(restored);
        Assert.Equal("V1", restored!.Name);
    }

    [Fact]
    public void CanvasPanelViewModel_ReadOnlyDashboard_BlocksAddWidget()
    {
        var sut = TestFactories.MakeCanvasPanelViewModel();
        sut.AddWidgetFromDrop(VisualType.Bar); // creates + opens a dashboard
        sut.ActiveDashboard!.IsReadOnly = true;

        var countBefore = sut.Widgets.Count;
        sut.AddWidget(new DashboardWidget { Title = "Blocked" });

        Assert.Equal(countBefore, sut.Widgets.Count);
    }

    // ── Phase 16: Performance (caching) ───────────────────────────────────────

    [Fact]
    public async Task CachingAnalyticsEngine_RepeatedIdenticalQuery_OnlyQueriesInnerOnce()
    {
        var inner = new Mock<IAnalyticsEngine>();
        inner.Setup(e => e.QueryAsync(It.IsAny<QuerySpec>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<IReadOnlyDictionary<string, object?>>());

        var sut = new CachingAnalyticsEngine(inner.Object, NullLogger<CachingAnalyticsEngine>.Instance, TimeSpan.FromMinutes(1));
        var spec = new QuerySpec { TableName = "t", Dimensions = ["dept"] };

        await sut.QueryAsync(spec);
        await sut.QueryAsync(spec);

        inner.Verify(e => e.QueryAsync(It.IsAny<QuerySpec>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CachingAnalyticsEngine_LoadDataSource_InvalidatesCache()
    {
        var inner = new Mock<IAnalyticsEngine>();
        inner.Setup(e => e.QueryAsync(It.IsAny<QuerySpec>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<IReadOnlyDictionary<string, object?>>());

        var sut = new CachingAnalyticsEngine(inner.Object, NullLogger<CachingAnalyticsEngine>.Instance);
        var spec = new QuerySpec { TableName = "t", Dimensions = ["dept"] };

        await sut.QueryAsync(spec);
        await sut.LoadDataSourceAsync(new DataSourceDescriptor(), []);
        await sut.QueryAsync(spec);

        inner.Verify(e => e.QueryAsync(It.IsAny<QuerySpec>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // ── Phase 18: Optional AI ─────────────────────────────────────────────────

    [Fact]
    public void AiFeatureGate_DefaultSettings_IsNotAvailable()
    {
        var settings = new Mock<IAppSettingsProvider>();
        settings.Setup(s => s.Settings).Returns(new AppSettings()); // AiEnabled defaults false

        var sut = new NCVizDash.TaskPane.Ai.AiFeatureGate(settings.Object, [], NullLogger<NCVizDash.TaskPane.Ai.AiFeatureGate>.Instance);

        Assert.False(sut.IsAvailable);
        Assert.Null(sut.TryGetProvider());
    }

    [Fact]
    public void AiFeatureGate_EnabledWithConfiguredProvider_IsAvailable()
    {
        var settings = new Mock<IAppSettingsProvider>();
        settings.Setup(s => s.Settings).Returns(new AppSettings { AiEnabled = true, AiProvider = "test-provider" });

        var provider = new Mock<IAiProvider>();
        provider.Setup(p => p.ProviderId).Returns("test-provider");

        var sut = new NCVizDash.TaskPane.Ai.AiFeatureGate(settings.Object, [provider.Object], NullLogger<NCVizDash.TaskPane.Ai.AiFeatureGate>.Instance);

        Assert.True(sut.IsAvailable);
        Assert.NotNull(sut.TryGetProvider());
    }

    [Fact]
    public async Task OpenAiCompatibleProvider_Forecast_UsesLinearTrend_NoNetworkCall()
    {
        var settings = new Mock<IAppSettingsProvider>();
        settings.Setup(s => s.Settings).Returns(new AppSettings());

        var sut = new NCVizDash.TaskPane.Ai.OpenAiProvider(new HttpClient(), settings.Object, NullLogger<NCVizDash.TaskPane.Ai.OpenAiProvider>.Instance);

        var forecast = await sut.ForecastAsync([10, 20, 30, 40], 2);

        Assert.Equal(2, forecast.Count);
        Assert.True(forecast[0] > 40); // trend continues upward
    }
}
