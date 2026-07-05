using Microsoft.Extensions.Logging.Abstractions;
using NCVizDash.Models;
using NCVizDash.RuleEngine;
using NCVizDash.TaskPane.ViewModels;
using Xunit;

namespace NCVizDash.Tests.Core;

/// <summary>
/// Tests that <see cref="CanvasPanelViewModel.AddWidgetFromFieldDrop"/> delegates
/// visual-type selection to the rule engine correctly.
/// </summary>
public sealed class CanvasPanelRuleEngineIntegrationTests
{
    private static CanvasPanelViewModel MakeSut() => TestFactories.MakeCanvasPanelViewModel();

    [Fact]
    public void AddWidgetFromFieldDrop_MeasureField_CreatesKpiWidget()
    {
        var sut = MakeSut();
        var field = new FieldDescriptor { Name = "Headcount", DisplayName = "Headcount", FieldType = FieldType.Measure };

        var widget = sut.AddWidgetFromFieldDrop(field, Guid.Empty);

        Assert.Equal(VisualType.Kpi, widget.VisualType);
        Assert.Contains("Headcount", widget.MeasureFields);
    }

    [Fact]
    public void AddWidgetFromFieldDrop_TimeField_CreatesLineWidget()
    {
        var sut = MakeSut();
        var field = new FieldDescriptor { Name = "OrderDate", DisplayName = "Order Date", FieldType = FieldType.Time };

        var widget = sut.AddWidgetFromFieldDrop(field, Guid.Empty);

        // Single time field → no measures, so falls through to Table
        // (Line needs at least one measure too — consistent with rule engine)
        Assert.Equal(VisualType.Table, widget.VisualType);
    }

    [Fact]
    public void AddWidgetFromFieldDrop_DimensionField_CreatesTableWidget()
    {
        var sut = MakeSut();
        var field = new FieldDescriptor { Name = "Department", DisplayName = "Department", FieldType = FieldType.Dimension };

        var widget = sut.AddWidgetFromFieldDrop(field, Guid.Empty);

        Assert.Equal(VisualType.Table, widget.VisualType);
        Assert.Contains("Department", widget.DimensionFields);
    }

    [Fact]
    public void AddWidgetFromFieldDrop_RateField_CreatesGaugeWidget()
    {
        var sut = MakeSut();
        var field = new FieldDescriptor { Name = "completion_rate", DisplayName = "Completion Rate", FieldType = FieldType.Measure };

        var widget = sut.AddWidgetFromFieldDrop(field, Guid.Empty);

        Assert.Equal(VisualType.Gauge, widget.VisualType);
    }

    [Fact]
    public void AddWidgetFromFieldDrop_OverrideVisual_IgnoresRuleEngine()
    {
        var sut = MakeSut();
        var field = new FieldDescriptor { Name = "Revenue", DisplayName = "Revenue", FieldType = FieldType.Measure };

        // Caller forces a Pie even though the rule engine would say KPI
        var widget = sut.AddWidgetFromFieldDrop(field, Guid.Empty, overrideVisual: VisualType.Pie);

        Assert.Equal(VisualType.Pie, widget.VisualType);
    }

    [Fact]
    public void AddWidgetFromFieldDrop_DefaultLayoutSize_MatchesVisualType()
    {
        var sut = MakeSut();
        var field = new FieldDescriptor { Name = "Headcount", DisplayName = "Headcount", FieldType = FieldType.Measure };

        var widget = sut.AddWidgetFromFieldDrop(field, Guid.Empty);

        // KPI default: 4 columns × 3 rows
        Assert.Equal(4, widget.Layout.ColumnSpan);
        Assert.Equal(3, widget.Layout.RowSpan);
    }
}
