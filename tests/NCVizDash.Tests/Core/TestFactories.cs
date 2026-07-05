using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NCVizDash.ChartEngine;
using NCVizDash.Core.Abstractions;
using NCVizDash.RuleEngine;
using NCVizDash.TaskPane.Services;
using NCVizDash.TaskPane.ViewModels;

namespace NCVizDash.Tests.Core;

/// <summary>
/// Shared construction helpers for ViewModels whose constructors take several
/// service dependencies. Centralising this avoids each test file re-wiring
/// the same NullLogger/Mock boilerplate.
/// </summary>
internal static class TestFactories
{
    /// <summary>Builds a real <see cref="DeterministicRuleEngine"/> with a null logger.</summary>
    public static DeterministicRuleEngine MakeRuleEngine() =>
        new(NullLogger<DeterministicRuleEngine>.Instance);

    /// <summary>Builds a real <see cref="CrossFilterManager"/> with no active filters.</summary>
    public static CrossFilterManager MakeFilterManager() =>
        new(NullLogger<CrossFilterManager>.Instance);

    /// <summary>Builds a real <see cref="GlobalFilterManager"/> with no dashboard bound.</summary>
    public static GlobalFilterManager MakeGlobalFilterManager() =>
        new(NullLogger<GlobalFilterManager>.Instance);

    /// <summary>Builds a real <see cref="UndoRedoManager"/> with empty history.</summary>
    public static UndoRedoManager MakeUndoRedoManager() =>
        new(NullLogger<UndoRedoManager>.Instance);

    /// <summary>Builds a real <see cref="BookmarkManager"/> bound to a fresh <see cref="GlobalFilterManager"/>.</summary>
    public static NCVizDash.TaskPane.Presentation.PresentationController MakePresentationController(IGlobalFilterManager? globalFilterManager = null) =>
        new(NullLogger<NCVizDash.TaskPane.Presentation.PresentationController>.Instance,
            new BookmarkManager(NullLogger<BookmarkManager>.Instance, globalFilterManager ?? MakeGlobalFilterManager()));

    /// <summary>Builds a mocked <see cref="IAnalyticsEngine"/> that reports no loaded data sources.</summary>
    public static Mock<IAnalyticsEngine> MakeAnalyticsEngineMock()
    {
        var mock = new Mock<IAnalyticsEngine>();
        mock.Setup(e => e.GetTableName(It.IsAny<Guid>())).Returns((string?)null);
        return mock;
    }

    /// <summary>Builds a <see cref="DistinctValueService"/> backed by a mocked <see cref="IAnalyticsEngine"/>.</summary>
    public static DistinctValueService MakeDistinctValueService(IAnalyticsEngine? analyticsEngine = null) =>
        new(analyticsEngine ?? MakeAnalyticsEngineMock().Object, NullLogger<DistinctValueService>.Instance);

    /// <summary>Builds a <see cref="WidgetRenderCoordinator"/> backed by a mocked <see cref="IAnalyticsEngine"/>.</summary>
    public static WidgetRenderCoordinator MakeRenderCoordinator(
        IFilterManager? filterManager = null,
        IGlobalFilterManager? globalFilterManager = null)
    {
        var analyticsEngine = MakeAnalyticsEngineMock();
        var chartEngine = new EChartsChartEngine(NullLogger<EChartsChartEngine>.Instance);

        return new WidgetRenderCoordinator(
            analyticsEngine.Object, chartEngine,
            filterManager ?? MakeFilterManager(),
            globalFilterManager ?? MakeGlobalFilterManager(),
            NullLogger<WidgetRenderCoordinator>.Instance);
    }

    /// <summary>Builds a <see cref="GlobalFilterBarViewModel"/> wired to a real <see cref="GlobalFilterManager"/>.</summary>
    public static GlobalFilterBarViewModel MakeGlobalFilterBarViewModel(IGlobalFilterManager? globalFilterManager = null) =>
        new(NullLogger<GlobalFilterBarViewModel>.Instance,
            globalFilterManager ?? MakeGlobalFilterManager(),
            MakeDistinctValueService());

    /// <summary>
    /// Builds a fully-wired <see cref="CanvasPanelViewModel"/> for tests that don't
    /// care about rule-engine, render-coordinator, or filter-manager behaviour specifically.
    /// </summary>
    public static CanvasPanelViewModel MakeCanvasPanelViewModel()
    {
        var filterManager = MakeFilterManager();
        var globalFilterManager = MakeGlobalFilterManager();

        return new(
            NullLogger<CanvasPanelViewModel>.Instance,
            MakeRuleEngine(),
            MakeRenderCoordinator(filterManager, globalFilterManager),
            filterManager,
            globalFilterManager,
            MakeGlobalFilterBarViewModel(globalFilterManager),
            MakeUndoRedoManager(),
            MakePresentationController(globalFilterManager));
    }
}
