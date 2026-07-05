using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NCVizDash.Core.Abstractions;
using NCVizDash.Models;
using NCVizDash.TaskPane.Services;
using NCVizDash.TaskPane.ViewModels;
using Xunit;

namespace NCVizDash.Tests.Core;

/// <summary>
/// Unit tests for <see cref="GlobalFilterBarViewModel"/>. Uses arbitrary,
/// non-business-specific field/table names throughout to verify the bar makes
/// no assumptions about what kind of data it's filtering.
/// </summary>
public sealed class GlobalFilterBarViewModelTests
{
    private static DataSourceDescriptor MakeSource(string name, params (string Field, FieldType Type)[] fields)
    {
        var ds = new DataSourceDescriptor { Name = name, SourceType = "ExcelTable", SheetName = "Sheet1" };
        foreach (var (field, type) in fields)
            ds.Fields.Add(new FieldDescriptor { Name = field, DisplayName = field, FieldType = type });
        return ds;
    }

    private static (Mock<IGlobalFilterManager> manager, GlobalFilterBarViewModel sut) MakeSut()
    {
        var manager = new Mock<IGlobalFilterManager>();
        manager.Setup(m => m.GetFilters()).Returns(new List<WidgetFilter>());

        var analyticsEngine = new Mock<IAnalyticsEngine>();
        analyticsEngine.Setup(e => e.GetTableName(It.IsAny<Guid>())).Returns((string?)null);
        var distinctValueService = new DistinctValueService(analyticsEngine.Object, NullLogger<DistinctValueService>.Instance);

        var sut = new GlobalFilterBarViewModel(
            NullLogger<GlobalFilterBarViewModel>.Instance, manager.Object, distinctValueService);

        return (manager, sut);
    }

    [Fact]
    public void RefreshAvailableFields_ArbitraryDataSource_PopulatesFieldOptions()
    {
        var (_, sut) = MakeSut();
        var source = MakeSource("MysteryTable", ("Widget Color", FieldType.Dimension), ("Widget Count", FieldType.Measure));

        sut.RefreshAvailableFields([source]);

        Assert.Equal(2, sut.AvailableFields.Count);
        Assert.Contains(sut.AvailableFields, f => f.Field.Name == "Widget Color");
        Assert.Contains(sut.AvailableFields, f => f.Field.Name == "Widget Count");
    }

    [Fact]
    public void RefreshAvailableFields_MultipleSources_CombinesAllFields()
    {
        var (_, sut) = MakeSut();
        var sourceA = MakeSource("TableA", ("FieldA", FieldType.Dimension));
        var sourceB = MakeSource("TableB", ("FieldB", FieldType.Time));

        sut.RefreshAvailableFields([sourceA, sourceB]);

        Assert.Equal(2, sut.AvailableFields.Count);
    }

    [Fact]
    public void RefreshAvailableFields_HiddenField_Excluded()
    {
        var (_, sut) = MakeSut();
        var source = MakeSource("T", ("Visible", FieldType.Dimension));
        source.Fields.Add(new FieldDescriptor { Name = "Hidden", DisplayName = "Hidden", FieldType = FieldType.Dimension, IsVisible = false });

        sut.RefreshAvailableFields([source]);

        Assert.Single(sut.AvailableFields);
        Assert.Equal("Visible", sut.AvailableFields[0].Field.Name);
    }

    [Fact]
    public void DisplayLabel_CombinesSourceAndFieldName()
    {
        var option = new GlobalFilterFieldOption
        {
            DataSourceName = "Orders",
            Field = new FieldDescriptor { DisplayName = "Ship Method" }
        };

        Assert.Equal("Orders › Ship Method", option.DisplayLabel);
    }

    [Fact]
    public void AddSelectedFilter_NoFieldSelected_DoesNothing()
    {
        var (manager, sut) = MakeSut();
        sut.SelectedValueForNewFilter = "SomeValue";

        sut.AddSelectedFilter();

        manager.Verify(m => m.AddOrUpdateFilter(It.IsAny<WidgetFilter>()), Times.Never);
    }

    [Fact]
    public void AddSelectedFilter_ValidSelection_CallsAddOrUpdateFilter_Generically()
    {
        var (manager, sut) = MakeSut();
        sut.SelectedFieldToAdd = new GlobalFilterFieldOption
        {
            DataSourceId = Guid.NewGuid(),
            DataSourceName = "AnySource",
            Field = new FieldDescriptor { Name = "ArbitraryDimension", DisplayName = "Arbitrary Dimension", FieldType = FieldType.Dimension }
        };
        sut.SelectedValueForNewFilter = "SomeArbitraryValue";

        sut.AddSelectedFilter();

        manager.Verify(m => m.AddOrUpdateFilter(It.Is<WidgetFilter>(f =>
            f.FieldName == "ArbitraryDimension" &&
            f.Operator == FilterOperator.Equals &&
            f.Values.Contains("SomeArbitraryValue"))), Times.Once);
    }

    [Fact]
    public void AddSelectedFilter_ClearsSelectedValueAfterAdding()
    {
        var (_, sut) = MakeSut();
        sut.SelectedFieldToAdd = new GlobalFilterFieldOption { Field = new FieldDescriptor { Name = "X" } };
        sut.SelectedValueForNewFilter = "Y";

        sut.AddSelectedFilter();

        Assert.Equal(string.Empty, sut.SelectedValueForNewFilter);
    }

    // ── Range filters (Measure fields — works for any numeric field) ─────────

    [Fact]
    public void AddRangeFilter_BothBounds_UsesBetween()
    {
        var (manager, sut) = MakeSut();

        sut.AddRangeFilter("AnyNumericField", 10, 100);

        manager.Verify(m => m.AddOrUpdateFilter(It.Is<WidgetFilter>(f =>
            f.FieldName == "AnyNumericField" &&
            f.Operator == FilterOperator.Between &&
            f.Values.Count == 2)), Times.Once);
    }

    [Fact]
    public void AddRangeFilter_OnlyMin_UsesGreaterThanOrEqual()
    {
        var (manager, sut) = MakeSut();

        sut.AddRangeFilter("AnyNumericField", 10, null);

        manager.Verify(m => m.AddOrUpdateFilter(It.Is<WidgetFilter>(f =>
            f.Operator == FilterOperator.GreaterThanOrEqual)), Times.Once);
    }

    [Fact]
    public void AddRangeFilter_OnlyMax_UsesLessThanOrEqual()
    {
        var (manager, sut) = MakeSut();

        sut.AddRangeFilter("AnyNumericField", null, 100);

        manager.Verify(m => m.AddOrUpdateFilter(It.Is<WidgetFilter>(f =>
            f.Operator == FilterOperator.LessThanOrEqual)), Times.Once);
    }

    [Fact]
    public void AddRangeFilter_NeitherBound_DoesNothing()
    {
        var (manager, sut) = MakeSut();

        sut.AddRangeFilter("AnyNumericField", null, null);

        manager.Verify(m => m.AddOrUpdateFilter(It.IsAny<WidgetFilter>()), Times.Never);
    }

    // ── Delegation to the manager ─────────────────────────────────────────────

    [Fact]
    public void RemoveFilter_DelegatesToManager()
    {
        var (manager, sut) = MakeSut();
        var filter = new WidgetFilter { FieldName = "X" };

        sut.RemoveFilter(filter);

        manager.Verify(m => m.RemoveFilter(filter.Id), Times.Once);
    }

    [Fact]
    public void ClearAll_DelegatesToManager()
    {
        var (manager, sut) = MakeSut();

        sut.ClearAll();

        manager.Verify(m => m.ClearAll(), Times.Once);
    }

    [Fact]
    public void ActiveFilters_PopulatedFromManager_AtConstruction()
    {
        var manager = new Mock<IGlobalFilterManager>();
        var filters = new List<WidgetFilter> { new() { FieldName = "X", Values = ["1"] } };
        manager.Setup(m => m.GetFilters()).Returns(filters);

        var analyticsEngine = new Mock<IAnalyticsEngine>();
        var distinctValueService = new DistinctValueService(analyticsEngine.Object, NullLogger<DistinctValueService>.Instance);
        var sut = new GlobalFilterBarViewModel(NullLogger<GlobalFilterBarViewModel>.Instance, manager.Object, distinctValueService);

        Assert.Single(sut.ActiveFilters);
    }
}
