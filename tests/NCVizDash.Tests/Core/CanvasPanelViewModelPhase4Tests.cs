using Microsoft.Extensions.Logging.Abstractions;
using NCVizDash.Models;
using NCVizDash.TaskPane.Geometry;
using NCVizDash.TaskPane.ViewModels;
using Xunit;

namespace NCVizDash.Tests.Core;

/// <summary>Unit tests for <see cref="CanvasPanelViewModel"/> Phase 4 operations.</summary>
public sealed class CanvasPanelViewModelPhase4Tests
{
    private static CanvasPanelViewModel MakeSut()
    {
        var sut = TestFactories.MakeCanvasPanelViewModel();
        sut.AddWidgetFromDrop(VisualType.Bar);
        return sut;
    }

    // ── MoveWidget ────────────────────────────────────────────────────────

    [Fact]
    public void MoveWidget_ValidPosition_UpdatesLayout()
    {
        var sut = MakeSut();
        var widget = sut.Widgets[0];

        sut.MoveWidget(widget, 4, 2);

        Assert.Equal(4, widget.Layout.Column);
        Assert.Equal(2, widget.Layout.Row);
    }

    [Fact]
    public void MoveWidget_NegativeColumn_ClampsToZero()
    {
        var sut = MakeSut();
        var widget = sut.Widgets[0];

        sut.MoveWidget(widget, -5, 0);

        Assert.Equal(0, widget.Layout.Column);
    }

    [Fact]
    public void MoveWidget_WithActiveDashboardBound_ClampsToGridWidth()
    {
        var sut = MakeSut();
        var widget = sut.Widgets[0];
        var span = widget.Layout.ColumnSpan;

        sut.MoveWidget(widget, 999, 0);

        // With 24-column grid: max valid column = 24 - span
        Assert.Equal(24 - span, widget.Layout.Column);
    }

    // ── ResizeWidget ──────────────────────────────────────────────────────

    [Fact]
    public void ResizeWidget_ValidSpan_UpdatesLayout()
    {
        var sut = MakeSut();
        var widget = sut.Widgets[0];

        sut.ResizeWidget(widget, 8, 5);

        Assert.Equal(8, widget.Layout.ColumnSpan);
        Assert.Equal(5, widget.Layout.RowSpan);
    }

    [Fact]
    public void ResizeWidget_BelowMinimum_ClampsToMinimum()
    {
        var sut = MakeSut();
        var widget = sut.Widgets[0];

        sut.ResizeWidget(widget, 0, 0);

        Assert.Equal(2, widget.Layout.ColumnSpan);
        Assert.Equal(2, widget.Layout.RowSpan);
    }

    // ── Selection ─────────────────────────────────────────────────────────

    [Fact]
    public void SelectWidget_NonAdditive_ClearsPreviousSelection()
    {
        var sut = TestFactories.MakeCanvasPanelViewModel();
        sut.AddWidgetFromDrop(VisualType.Bar);
        sut.AddWidgetFromDrop(VisualType.Line);

        var first = sut.Widgets[0];
        var second = sut.Widgets[1];

        sut.SelectWidget(first, additive: false);
        sut.SelectWidget(second, additive: false);

        Assert.Single(sut.SelectedWidgets);
        Assert.Equal(second, sut.SelectedWidget);
        Assert.False(first.IsSelected);
        Assert.True(second.IsSelected);
    }

    [Fact]
    public void SelectWidget_Additive_BuildsMultiSelection()
    {
        var sut = TestFactories.MakeCanvasPanelViewModel();
        sut.AddWidgetFromDrop(VisualType.Bar);
        sut.AddWidgetFromDrop(VisualType.Line);

        sut.SelectWidget(sut.Widgets[0], additive: false);
        sut.SelectWidget(sut.Widgets[1], additive: true);

        Assert.Equal(2, sut.SelectedWidgets.Count);
        Assert.True(sut.Widgets[0].IsSelected);
        Assert.True(sut.Widgets[1].IsSelected);
    }

    [Fact]
    public void SelectWidget_Additive_TogglesOutExistingSelection()
    {
        var sut = MakeSut();
        var widget = sut.Widgets[0];

        sut.SelectWidget(widget, additive: false);
        sut.SelectWidget(widget, additive: true); // toggle off

        Assert.Empty(sut.SelectedWidgets);
        Assert.False(widget.IsSelected);
    }

    [Fact]
    public void ClearSelection_ResetsAllFlags()
    {
        var sut = TestFactories.MakeCanvasPanelViewModel();
        sut.AddWidgetFromDrop(VisualType.Bar);
        sut.AddWidgetFromDrop(VisualType.Line);

        sut.SelectWidget(sut.Widgets[0], additive: false);
        sut.SelectWidget(sut.Widgets[1], additive: true);
        sut.ClearSelection();

        Assert.Empty(sut.SelectedWidgets);
        Assert.Null(sut.SelectedWidget);
        Assert.All(sut.Widgets, w => Assert.False(w.IsSelected));
    }

    // ── DuplicateWidget ───────────────────────────────────────────────────

    [Fact]
    public void DuplicateWidget_ProducesNewIdWithSameVisualType()
    {
        var sut = MakeSut();
        var source = sut.Widgets[0];

        var copy = sut.DuplicateWidget(source);

        Assert.NotEqual(source.Id, copy.Id);
        Assert.Equal(source.VisualType, copy.VisualType);
        Assert.Contains("(Copy)", copy.Title);
    }

    [Fact]
    public void DuplicateWidget_CopiesLocalFilters_Independently()
    {
        var sut = MakeSut();
        var source = sut.Widgets[0];
        source.LocalFilters.Add(new WidgetFilter { FieldName = "Quarter", Operator = FilterOperator.NotIn, Values = ["Q1"] });

        var copy = sut.DuplicateWidget(source);
        copy.LocalFilters[0].Values.Clear(); // mutate copy

        Assert.Single(source.LocalFilters[0].Values); // original untouched
    }

    [Fact]
    public void DuplicateWidget_IsAddedToCanvasAndDashboard()
    {
        var sut = MakeSut();
        var source = sut.Widgets[0];

        var copy = sut.DuplicateWidget(source);

        Assert.Contains(copy, sut.Widgets);
        Assert.Contains(copy, sut.ActiveDashboard!.Widgets);
    }

    [Fact]
    public void DeleteSelectedWidget_MultiSelect_DeletesAll()
    {
        var sut = TestFactories.MakeCanvasPanelViewModel();
        sut.AddWidgetFromDrop(VisualType.Bar);
        sut.AddWidgetFromDrop(VisualType.Line);

        sut.SelectWidget(sut.Widgets[0], additive: false);
        sut.SelectWidget(sut.Widgets[1], additive: true);

        sut.DeleteSelectedWidget();

        Assert.Empty(sut.Widgets);
    }

    // ── Alignment guides ──────────────────────────────────────────────────

    [Fact]
    public void UpdateGuides_WithAlignedNeighbour_PopulatesActiveGuides()
    {
        var sut = TestFactories.MakeCanvasPanelViewModel();
        sut.AddWidgetFromDrop(VisualType.Bar);
        sut.AddWidgetFromDrop(VisualType.Line);

        // Align both widgets' left edges at column 0.
        sut.Widgets[0].Layout.Column = 0;
        sut.Widgets[1].Layout.Column = 0;

        sut.UpdateGuides(sut.Widgets[0]);

        Assert.NotEmpty(sut.ActiveGuides);
    }

    [Fact]
    public void ClearGuides_EmptiesActiveGuides()
    {
        var sut = TestFactories.MakeCanvasPanelViewModel();
        sut.AddWidgetFromDrop(VisualType.Bar);
        sut.ActiveGuides.Add(new AlignmentGuide(GuideOrientation.Vertical, 5));

        sut.ClearGuides();

        Assert.Empty(sut.ActiveGuides);
    }
}
