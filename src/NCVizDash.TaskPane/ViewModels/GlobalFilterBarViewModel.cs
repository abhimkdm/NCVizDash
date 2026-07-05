using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NCVizDash.Core.Abstractions;
using NCVizDash.Models;
using System.Collections.ObjectModel;

namespace NCVizDash.TaskPane.ViewModels;

/// <summary>
/// A single pickable field for the global filter bar's "Add Filter" flow.
/// Combines a <see cref="FieldDescriptor"/> with the data source it came from,
/// since the same display name could theoretically exist on two different sources.
/// </summary>
public sealed class GlobalFilterFieldOption
{
    public Guid DataSourceId { get; init; }
    public string DataSourceName { get; init; } = string.Empty;
    public FieldDescriptor Field { get; init; } = new();

    /// <summary>Combined label shown in the field picker, e.g. "Sales › Region".</summary>
    public string DisplayLabel => $"{DataSourceName} › {Field.DisplayName}";
}

/// <summary>
/// Drives the dashboard-wide global filter bar. Entirely data-agnostic: the set of
/// fields offered comes from whatever data sources are actually loaded, so it works
/// identically whether the workbook contains sales data, HR data, project data, or
/// anything else — there is no hardcoded field list.
/// </summary>
public sealed partial class GlobalFilterBarViewModel : ObservableObject
{
    private readonly ILogger<GlobalFilterBarViewModel> _logger;
    private readonly IGlobalFilterManager _globalFilterManager;
    private readonly DistinctValueService _distinctValueService;

    /// <summary>Every filterable field across every currently loaded data source.</summary>
    public ObservableCollection<GlobalFilterFieldOption> AvailableFields { get; } = [];

    /// <summary>The currently active dashboard-wide filters (mirrors <see cref="IGlobalFilterManager.GetFilters"/>).</summary>
    public ObservableCollection<WidgetFilter> ActiveFilters { get; } = [];

    [ObservableProperty]
    private GlobalFilterFieldOption? _selectedFieldToAdd;

    [ObservableProperty]
    private ObservableCollection<string> _valueOptionsForSelectedField = [];

    [ObservableProperty]
    private string _selectedValueForNewFilter = string.Empty;

    [ObservableProperty]
    private bool _isLoadingValues;

    /// <summary>Initialises the filter bar and subscribes to filter-state changes.</summary>
    public GlobalFilterBarViewModel(
        ILogger<GlobalFilterBarViewModel> logger,
        IGlobalFilterManager globalFilterManager,
        DistinctValueService distinctValueService)
    {
        _logger = logger;
        _globalFilterManager = globalFilterManager;
        _distinctValueService = distinctValueService;

        _globalFilterManager.FiltersChanged += (_, _) => RefreshActiveFilters();
        RefreshActiveFilters();
    }

    /// <summary>
    /// Rebuilds <see cref="AvailableFields"/> from the given set of loaded data sources.
    /// Call this whenever the Explorer's data source list changes (initial load, refresh,
    /// or a new source added) so the field picker always reflects what's actually queryable.
    /// </summary>
    public void RefreshAvailableFields(IEnumerable<DataSourceDescriptor> dataSources)
    {
        AvailableFields.Clear();

        foreach (var source in dataSources)
        {
            foreach (var field in source.Fields.Where(f => f.IsVisible))
            {
                AvailableFields.Add(new GlobalFilterFieldOption
                {
                    DataSourceId = source.Id,
                    DataSourceName = source.Name,
                    Field = field
                });
            }
        }

        _logger.LogDebug("Global filter field picker refreshed: {Count} field(s) available.", AvailableFields.Count);
    }

    /// <summary>
    /// Loads the distinct value list for <see cref="SelectedFieldToAdd"/>, ready for
    /// the user to pick one and commit via <see cref="AddSelectedFilter"/>.
    /// For Measure fields (no meaningful "distinct list"), this is a no-op — numeric
    /// filters are entered directly rather than picked from a list.
    /// </summary>
    [RelayCommand]
    public async Task LoadValueOptionsAsync()
    {
        ValueOptionsForSelectedField.Clear();

        if (SelectedFieldToAdd is null || SelectedFieldToAdd.Field.FieldType == FieldType.Measure)
            return;

        IsLoadingValues = true;
        try
        {
            var values = await _distinctValueService.GetDistinctValuesAsync(
                SelectedFieldToAdd.DataSourceId, SelectedFieldToAdd.Field.Name);

            foreach (var v in values)
                ValueOptionsForSelectedField.Add(v);
        }
        finally
        {
            IsLoadingValues = false;
        }
    }

    /// <summary>
    /// Commits a new global filter for <see cref="SelectedFieldToAdd"/> using
    /// <see cref="SelectedValueForNewFilter"/> as an Equals match. Works identically
    /// regardless of the field's origin or business meaning — this is intentionally
    /// generic rather than special-cased per field name.
    /// </summary>
    [RelayCommand]
    public void AddSelectedFilter()
    {
        if (SelectedFieldToAdd is null || string.IsNullOrWhiteSpace(SelectedValueForNewFilter))
            return;

        _globalFilterManager.AddOrUpdateFilter(new WidgetFilter
        {
            FieldName = SelectedFieldToAdd.Field.Name,
            Operator = FilterOperator.Equals,
            Values = [SelectedValueForNewFilter]
        });

        SelectedValueForNewFilter = string.Empty;
    }

    /// <summary>
    /// Commits a numeric range filter for a Measure field. Uses Between when both
    /// bounds are supplied, or a one-sided comparison when only one is.
    /// </summary>
    public void AddRangeFilter(string field, double? min, double? max)
    {
        if (string.IsNullOrWhiteSpace(field) || (min is null && max is null))
            return;

        var filter = new WidgetFilter { FieldName = field };

        if (min is not null && max is not null)
        {
            filter.Operator = FilterOperator.Between;
            filter.Values = [min.Value.ToString(), max.Value.ToString()];
        }
        else if (min is not null)
        {
            filter.Operator = FilterOperator.GreaterThanOrEqual;
            filter.Values = [min.Value.ToString()];
        }
        else
        {
            filter.Operator = FilterOperator.LessThanOrEqual;
            filter.Values = [max!.Value.ToString()];
        }

        _globalFilterManager.AddOrUpdateFilter(filter);
    }

    /// <summary>Removes a single active filter.</summary>
    [RelayCommand]
    public void RemoveFilter(WidgetFilter filter) => _globalFilterManager.RemoveFilter(filter.Id);

    /// <summary>Removes every active global filter.</summary>
    [RelayCommand]
    public void ClearAll() => _globalFilterManager.ClearAll();

    // ── Private ───────────────────────────────────────────────────────────────

    private void RefreshActiveFilters()
    {
        ActiveFilters.Clear();
        foreach (var filter in _globalFilterManager.GetFilters())
            ActiveFilters.Add(filter);
    }
}
