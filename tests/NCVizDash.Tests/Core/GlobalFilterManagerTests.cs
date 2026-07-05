using Microsoft.Extensions.Logging.Abstractions;
using NCVizDash.Models;
using NCVizDash.TaskPane.Services;
using Xunit;

namespace NCVizDash.Tests.Core;

/// <summary>Unit tests for <see cref="GlobalFilterManager"/>.</summary>
public sealed class GlobalFilterManagerTests
{
    private static GlobalFilterManager MakeSut() => new(NullLogger<GlobalFilterManager>.Instance);

    [Fact]
    public void GetFilters_NoDashboardBound_ReturnsEmpty()
    {
        var sut = MakeSut();
        Assert.Empty(sut.GetFilters());
    }

    [Fact]
    public void AddOrUpdateFilter_NoDashboardBound_IsIgnoredSafely()
    {
        var sut = MakeSut();
        var exception = Record.Exception(() =>
            sut.AddOrUpdateFilter(new WidgetFilter { FieldName = "Region", Values = ["EMEA"] }));

        Assert.Null(exception);
        Assert.Empty(sut.GetFilters());
    }

    [Fact]
    public void SetDashboard_BindsDashboardAndExposesExistingFilters()
    {
        var sut = MakeSut();
        var dashboard = new Dashboard();
        dashboard.GlobalFilters.Add(new WidgetFilter { FieldName = "Region", Values = ["EMEA"] });

        sut.SetDashboard(dashboard);

        Assert.Single(sut.GetFilters());
        Assert.Equal(dashboard, sut.ActiveDashboard);
    }

    [Fact]
    public void SetDashboard_RaisesFiltersChanged()
    {
        var sut = MakeSut();
        var raised = false;
        sut.FiltersChanged += (_, _) => raised = true;

        sut.SetDashboard(new Dashboard());

        Assert.True(raised);
    }

    [Fact]
    public void SetDashboard_SameDashboardTwice_DoesNotRaiseFiltersChangedTwice()
    {
        var sut = MakeSut();
        var dashboard = new Dashboard();
        sut.SetDashboard(dashboard);

        var raised = false;
        sut.FiltersChanged += (_, _) => raised = true;
        sut.SetDashboard(dashboard);

        Assert.False(raised);
    }

    [Fact]
    public void AddOrUpdateFilter_NewFilter_AddsToActiveDashboard()
    {
        var sut = MakeSut();
        var dashboard = new Dashboard();
        sut.SetDashboard(dashboard);

        sut.AddOrUpdateFilter(new WidgetFilter { FieldName = "Department", Values = ["Engineering"] });

        Assert.Single(dashboard.GlobalFilters);
        Assert.Single(sut.GetFilters());
    }

    [Fact]
    public void AddOrUpdateFilter_ExistingId_ReplacesInPlace()
    {
        var sut = MakeSut();
        var dashboard = new Dashboard();
        sut.SetDashboard(dashboard);

        var filter = new WidgetFilter { FieldName = "Department", Values = ["Engineering"] };
        sut.AddOrUpdateFilter(filter);

        var updated = new WidgetFilter { Id = filter.Id, FieldName = "Department", Values = ["Sales"] };
        sut.AddOrUpdateFilter(updated);

        Assert.Single(sut.GetFilters());
        Assert.Equal(["Sales"], sut.GetFilters()[0].Values);
    }

    [Fact]
    public void AddOrUpdateFilter_ArbitraryFieldName_WorksGenerically()
    {
        // Proves the manager makes no assumption about field names/domains —
        // any string works, matching the "dynamic filters for any kind of data" goal.
        var sut = MakeSut();
        sut.SetDashboard(new Dashboard());

        sut.AddOrUpdateFilter(new WidgetFilter { FieldName = "Custom_Field_XYZ_123", Values = ["whatever"] });

        Assert.Equal("Custom_Field_XYZ_123", sut.GetFilters()[0].FieldName);
    }

    [Fact]
    public void RemoveFilter_ExistingFilter_RemovesIt()
    {
        var sut = MakeSut();
        var dashboard = new Dashboard();
        sut.SetDashboard(dashboard);

        var filter = new WidgetFilter { FieldName = "Region", Values = ["EMEA"] };
        sut.AddOrUpdateFilter(filter);
        sut.RemoveFilter(filter.Id);

        Assert.Empty(sut.GetFilters());
    }

    [Fact]
    public void RemoveFilter_NonExistentId_DoesNotRaiseFiltersChanged()
    {
        var sut = MakeSut();
        sut.SetDashboard(new Dashboard());

        var raised = false;
        sut.FiltersChanged += (_, _) => raised = true;
        sut.RemoveFilter(Guid.NewGuid());

        Assert.False(raised);
    }

    [Fact]
    public void SetFilterEnabled_DisablesWithoutRemoving()
    {
        var sut = MakeSut();
        var dashboard = new Dashboard();
        sut.SetDashboard(dashboard);

        var filter = new WidgetFilter { FieldName = "Region", Values = ["EMEA"] };
        sut.AddOrUpdateFilter(filter);
        sut.SetFilterEnabled(filter.Id, false);

        Assert.Single(sut.GetFilters());       // still present
        Assert.Empty(sut.GetEnabledFilters());  // but not enabled
    }

    [Fact]
    public void GetEnabledFilters_OnlyReturnsEnabledOnes()
    {
        var sut = MakeSut();
        var dashboard = new Dashboard();
        sut.SetDashboard(dashboard);

        sut.AddOrUpdateFilter(new WidgetFilter { FieldName = "A", Values = ["1"], IsEnabled = true });
        sut.AddOrUpdateFilter(new WidgetFilter { FieldName = "B", Values = ["2"], IsEnabled = false });

        Assert.Single(sut.GetEnabledFilters());
        Assert.Equal("A", sut.GetEnabledFilters()[0].FieldName);
    }

    [Fact]
    public void ClearAll_RemovesEveryFilter()
    {
        var sut = MakeSut();
        var dashboard = new Dashboard();
        sut.SetDashboard(dashboard);

        sut.AddOrUpdateFilter(new WidgetFilter { FieldName = "A", Values = ["1"] });
        sut.AddOrUpdateFilter(new WidgetFilter { FieldName = "B", Values = ["2"] });
        sut.ClearAll();

        Assert.Empty(sut.GetFilters());
    }

    [Fact]
    public void ClearAll_EmptyList_DoesNotRaiseFiltersChanged()
    {
        var sut = MakeSut();
        sut.SetDashboard(new Dashboard());

        var raised = false;
        sut.FiltersChanged += (_, _) => raised = true;
        sut.ClearAll();

        Assert.False(raised);
    }

    [Fact]
    public void AddOrUpdateFilter_UpdatesDashboardModifiedAt()
    {
        var sut = MakeSut();
        var dashboard = new Dashboard();
        var before = dashboard.ModifiedAt;
        sut.SetDashboard(dashboard);

        sut.AddOrUpdateFilter(new WidgetFilter { FieldName = "Region", Values = ["EMEA"] });

        Assert.True(dashboard.ModifiedAt >= before);
    }

    [Fact]
    public void SwitchingDashboards_ExposesNewDashboardsFilters()
    {
        var sut = MakeSut();
        var dashboardA = new Dashboard();
        dashboardA.GlobalFilters.Add(new WidgetFilter { FieldName = "A", Values = ["1"] });

        var dashboardB = new Dashboard();
        dashboardB.GlobalFilters.Add(new WidgetFilter { FieldName = "B", Values = ["2"] });

        sut.SetDashboard(dashboardA);
        Assert.Equal("A", sut.GetFilters()[0].FieldName);

        sut.SetDashboard(dashboardB);
        Assert.Equal("B", sut.GetFilters()[0].FieldName);
    }
}
