using NCVizDash.Models;
using System.Text.Json;
using Xunit;

namespace NCVizDash.Tests.Core;

/// <summary>Unit tests for <see cref="WidgetFilter"/> and <see cref="DashboardWidget.LocalFilters"/>.</summary>
public sealed class WidgetFilterTests
{
    [Fact]
    public void DashboardWidget_LocalFilters_DefaultsToEmpty()
    {
        var widget = new DashboardWidget();
        Assert.Empty(widget.LocalFilters);
    }

    [Fact]
    public void WidgetFilter_DefaultsToEqualsOperatorAndEnabled()
    {
        var filter = new WidgetFilter { FieldName = "Quarter" };

        Assert.Equal(FilterOperator.Equals, filter.Operator);
        Assert.True(filter.IsEnabled);
        Assert.Empty(filter.Values);
    }

    [Fact]
    public void WidgetFilter_CanBeAddedToWidget_AndSurvivesRoundTrip()
    {
        var widget = new DashboardWidget { Title = "Revenue by Quarter" };
        widget.LocalFilters.Add(new WidgetFilter
        {
            FieldName = "Quarter",
            Operator = FilterOperator.NotIn,
            Values = ["Q1"]
        });

        var json = JsonSerializer.Serialize(widget);
        var deserialized = JsonSerializer.Deserialize<DashboardWidget>(json);

        Assert.NotNull(deserialized);
        Assert.Single(deserialized!.LocalFilters);
        Assert.Equal("Quarter", deserialized.LocalFilters[0].FieldName);
        Assert.Equal(FilterOperator.NotIn, deserialized.LocalFilters[0].Operator);
        Assert.Equal(["Q1"], deserialized.LocalFilters[0].Values);
    }

    [Fact]
    public void DashboardWidget_IsSelected_NotIncludedInJsonSerialization()
    {
        var widget = new DashboardWidget { Title = "Test", IsSelected = true };

        var json = JsonSerializer.Serialize(widget);

        Assert.DoesNotContain("IsSelected", json);
    }

    [Theory]
    [InlineData(FilterOperator.Equals)]
    [InlineData(FilterOperator.Between)]
    [InlineData(FilterOperator.In)]
    [InlineData(FilterOperator.Contains)]
    public void WidgetFilter_Operator_SerializesAsStringEnum(FilterOperator op)
    {
        var filter = new WidgetFilter { FieldName = "X", Operator = op };

        var json = JsonSerializer.Serialize(filter);

        Assert.Contains(op.ToString(), json);
    }
}
