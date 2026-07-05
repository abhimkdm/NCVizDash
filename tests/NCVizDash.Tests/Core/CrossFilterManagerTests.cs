using Microsoft.Extensions.Logging.Abstractions;
using NCVizDash.Models;
using NCVizDash.TaskPane.Services;
using Xunit;

namespace NCVizDash.Tests.Core;

/// <summary>Unit tests for <see cref="CrossFilterManager"/>.</summary>
public sealed class CrossFilterManagerTests
{
    private static CrossFilterManager MakeSut() => new(NullLogger<CrossFilterManager>.Instance);

    [Fact]
    public void ApplyFilter_NewField_AddsActiveFilter()
    {
        var sut = MakeSut();
        var widgetId = Guid.NewGuid();

        sut.ApplyFilter(widgetId, "Region", ["EMEA"]);

        Assert.Equal(1, sut.ActiveFilterCount);
        var filters = sut.GetActiveFilters();
        Assert.Single(filters);
        Assert.Equal("Region", filters[0].FieldName);
        Assert.Equal(["EMEA"], filters[0].Values);
    }

    [Fact]
    public void ApplyFilter_UsesEqualsOperator_ForSingleValue()
    {
        var sut = MakeSut();
        sut.ApplyFilter(Guid.NewGuid(), "Region", ["EMEA"]);

        Assert.Equal(FilterOperator.Equals, sut.GetActiveFilters()[0].Operator);
    }

    [Fact]
    public void ApplyFilter_UsesInOperator_ForMultipleValues()
    {
        var sut = MakeSut();
        sut.ApplyFilter(Guid.NewGuid(), "Region", ["EMEA", "APAC"]);

        Assert.Equal(FilterOperator.In, sut.GetActiveFilters()[0].Operator);
    }

    [Fact]
    public void ApplyFilter_SameWidgetSameValue_TogglesOff()
    {
        var sut = MakeSut();
        var widgetId = Guid.NewGuid();

        sut.ApplyFilter(widgetId, "Region", ["EMEA"]);
        sut.ApplyFilter(widgetId, "Region", ["EMEA"]); // click same bar again

        Assert.Equal(0, sut.ActiveFilterCount);
    }

    [Fact]
    public void ApplyFilter_SameWidgetDifferentValue_Replaces()
    {
        var sut = MakeSut();
        var widgetId = Guid.NewGuid();

        sut.ApplyFilter(widgetId, "Region", ["EMEA"]);
        sut.ApplyFilter(widgetId, "Region", ["APAC"]);

        Assert.Equal(1, sut.ActiveFilterCount);
        Assert.Equal(["APAC"], sut.GetActiveFilters()[0].Values);
    }

    [Fact]
    public void ApplyFilter_DifferentWidgetSameField_Overwrites()
    {
        var sut = MakeSut();
        var widgetA = Guid.NewGuid();
        var widgetB = Guid.NewGuid();

        sut.ApplyFilter(widgetA, "Region", ["EMEA"]);
        sut.ApplyFilter(widgetB, "Region", ["APAC"]);

        Assert.Equal(1, sut.ActiveFilterCount);
        Assert.Equal(["APAC"], sut.GetActiveFilters()[0].Values);
    }

    [Fact]
    public void ApplyFilter_EmptyValues_ClearsField()
    {
        var sut = MakeSut();
        var widgetId = Guid.NewGuid();

        sut.ApplyFilter(widgetId, "Region", ["EMEA"]);
        sut.ApplyFilter(widgetId, "Region", []);

        Assert.Equal(0, sut.ActiveFilterCount);
    }

    [Fact]
    public void ApplyFilter_MultipleFields_TrackedIndependently()
    {
        var sut = MakeSut();
        var widgetId = Guid.NewGuid();

        sut.ApplyFilter(widgetId, "Region", ["EMEA"]);
        sut.ApplyFilter(widgetId, "Department", ["Engineering"]);

        Assert.Equal(2, sut.ActiveFilterCount);
    }

    [Fact]
    public void ApplyFilter_EmptyFieldName_Ignored()
    {
        var sut = MakeSut();
        sut.ApplyFilter(Guid.NewGuid(), "", ["EMEA"]);

        Assert.Equal(0, sut.ActiveFilterCount);
    }

    [Fact]
    public void ApplyFilter_RaisesFiltersChanged()
    {
        var sut = MakeSut();
        var raised = false;
        sut.FiltersChanged += (_, _) => raised = true;

        sut.ApplyFilter(Guid.NewGuid(), "Region", ["EMEA"]);

        Assert.True(raised);
    }

    [Fact]
    public void ApplyFilter_NoActualChange_DoesNotRaiseFiltersChanged()
    {
        var sut = MakeSut();
        sut.ApplyFilter(Guid.NewGuid(), "Region", []); // clearing an already-empty field

        var raised = false;
        sut.FiltersChanged += (_, _) => raised = true;
        sut.ApplyFilter(Guid.NewGuid(), "NonExistentField", []);

        Assert.False(raised);
    }

    [Fact]
    public void ClearAll_RemovesEveryFilter()
    {
        var sut = MakeSut();
        sut.ApplyFilter(Guid.NewGuid(), "Region", ["EMEA"]);
        sut.ApplyFilter(Guid.NewGuid(), "Department", ["Eng"]);

        sut.ClearAll();

        Assert.Equal(0, sut.ActiveFilterCount);
    }

    [Fact]
    public void ClearAll_RaisesFiltersChanged_WhenFiltersExisted()
    {
        var sut = MakeSut();
        sut.ApplyFilter(Guid.NewGuid(), "Region", ["EMEA"]);

        var raised = false;
        sut.FiltersChanged += (_, _) => raised = true;
        sut.ClearAll();

        Assert.True(raised);
    }

    [Fact]
    public void ClearAll_NoActiveFilters_DoesNotRaiseFiltersChanged()
    {
        var sut = MakeSut();
        var raised = false;
        sut.FiltersChanged += (_, _) => raised = true;

        sut.ClearAll();

        Assert.False(raised);
    }

    // ── Self-exclusion ────────────────────────────────────────────────────────

    [Fact]
    public void GetActiveFilters_ExcludeSourceWidgetId_OmitsOwnFilter()
    {
        var sut = MakeSut();
        var widgetA = Guid.NewGuid();
        var widgetB = Guid.NewGuid();

        sut.ApplyFilter(widgetA, "Region", ["EMEA"]);
        sut.ApplyFilter(widgetB, "Department", ["Eng"]);

        var filtersForA = sut.GetActiveFilters(excludeSourceWidgetId: widgetA);

        Assert.Single(filtersForA);
        Assert.Equal("Department", filtersForA[0].FieldName);
    }

    [Fact]
    public void GetActiveFilters_NoExclusion_ReturnsAll()
    {
        var sut = MakeSut();
        sut.ApplyFilter(Guid.NewGuid(), "Region", ["EMEA"]);
        sut.ApplyFilter(Guid.NewGuid(), "Department", ["Eng"]);

        var filters = sut.GetActiveFilters();

        Assert.Equal(2, filters.Count);
    }

    // ── BuildWhereClause ──────────────────────────────────────────────────────

    [Fact]
    public void BuildWhereClause_SingleFilter_ProducesFragment()
    {
        var sut = MakeSut();
        sut.ApplyFilter(Guid.NewGuid(), "Region", ["EMEA"]);

        var clause = sut.BuildWhereClause();

        Assert.Contains("\"region\" = 'EMEA'", clause);
        Assert.DoesNotContain("WHERE", clause); // fragment only, no keyword
    }

    [Fact]
    public void BuildWhereClause_NoFilters_ReturnsEmptyString()
    {
        var sut = MakeSut();
        Assert.Equal(string.Empty, sut.BuildWhereClause());
    }

    [Fact]
    public void BuildWhereClause_MultipleFilters_JoinedWithAnd()
    {
        var sut = MakeSut();
        sut.ApplyFilter(Guid.NewGuid(), "Region", ["EMEA"]);
        sut.ApplyFilter(Guid.NewGuid(), "Department", ["Eng"]);

        var clause = sut.BuildWhereClause();

        Assert.Contains(" AND ", clause);
    }
}
