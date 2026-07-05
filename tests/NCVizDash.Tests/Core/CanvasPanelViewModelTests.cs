using Microsoft.Extensions.Logging.Abstractions;
using NCVizDash.Models;
using NCVizDash.TaskPane.ViewModels;
using Xunit;

namespace NCVizDash.Tests.Core;

/// <summary>Unit tests for <see cref="CanvasPanelViewModel"/>.</summary>
public sealed class CanvasPanelViewModelTests
{
    private static CanvasPanelViewModel MakeSut() =>
        TestFactories.MakeCanvasPanelViewModel();

    [Fact]
    public void AddWidgetFromDrop_NoActiveDashboard_CreatesDefaultDashboard()
    {
        var sut = MakeSut();

        sut.AddWidgetFromDrop(VisualType.Bar);

        Assert.NotNull(sut.ActiveDashboard);
        Assert.Single(sut.ActiveDashboard!.Widgets);
    }

    [Fact]
    public void AddWidgetFromDrop_WithoutField_UsesVisualTypeAsTitle()
    {
        var sut = MakeSut();

        var widget = sut.AddWidgetFromDrop(VisualType.Pie);

        Assert.Equal("Pie", widget.Title);
        Assert.Equal(VisualType.Pie, widget.VisualType);
        Assert.Empty(widget.MeasureFields);
        Assert.Empty(widget.DimensionFields);
    }

    [Fact]
    public void AddWidgetFromDrop_WithMeasureField_AddsToMeasureFields()
    {
        var sut = MakeSut();
        var field = new FieldDescriptor { Name = "Revenue", DisplayName = "Revenue", FieldType = FieldType.Measure };

        var widget = sut.AddWidgetFromDrop(VisualType.Kpi, field);

        Assert.Contains("Revenue", widget.MeasureFields);
        Assert.Empty(widget.DimensionFields);
        Assert.Contains("Revenue", widget.Title);
    }

    [Fact]
    public void AddWidgetFromDrop_WithDimensionField_AddsToDimensionFields()
    {
        var sut = MakeSut();
        var field = new FieldDescriptor { Name = "Department", DisplayName = "Department", FieldType = FieldType.Dimension };

        var widget = sut.AddWidgetFromDrop(VisualType.Bar, field);

        Assert.Contains("Department", widget.DimensionFields);
        Assert.Empty(widget.MeasureFields);
    }

    [Fact]
    public void AddWidgetFromDrop_AddsWidgetToCanvasAndDashboard()
    {
        var sut = MakeSut();

        var widget = sut.AddWidgetFromDrop(VisualType.Line);

        Assert.Contains(widget, sut.Widgets);
        Assert.Contains(widget, sut.ActiveDashboard!.Widgets);
        Assert.Equal(widget, sut.SelectedWidget);
    }

    [Fact]
    public void DeleteSelectedWidget_RemovesFromCanvasAndDashboard()
    {
        var sut = MakeSut();
        var widget = sut.AddWidgetFromDrop(VisualType.Area);

        sut.DeleteSelectedWidget();

        Assert.DoesNotContain(widget, sut.Widgets);
        Assert.DoesNotContain(widget, sut.ActiveDashboard!.Widgets);
        Assert.Null(sut.SelectedWidget);
    }

    [Fact]
    public void DeleteSelectedWidget_NoSelection_DoesNothing()
    {
        var sut = MakeSut();
        sut.AddWidgetFromDrop(VisualType.Area);
        sut.SelectedWidget = null;

        var exception = Record.Exception(() => sut.DeleteSelectedWidget());

        Assert.Null(exception);
        Assert.Single(sut.Widgets);
    }
}
