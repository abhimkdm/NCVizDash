using Microsoft.Extensions.Logging;
using NCVizDash.Models;

namespace NCVizDash.TaskPane.Templates;

/// <summary>Result of instantiating a template: the dashboard built, plus any slots that couldn't be auto-filled.</summary>
public sealed record TemplateInstantiationResult(Dashboard Dashboard, IReadOnlyList<TemplateWidgetSlot> UnfilledSlots)
{
    /// <summary>True when every slot was filled automatically — no follow-up questions needed.</summary>
    public bool IsComplete => UnfilledSlots.Count == 0;
}

/// <summary>
/// Turns a <see cref="DashboardTemplate"/> into a real <see cref="Dashboard"/> by
/// matching each slot's field-count requirements against the fields actually
/// available on a chosen data source — a best-effort, greedy match (first N
/// unused measures/dimensions fill each slot in slot order). If a data source
/// doesn't have enough fields of the right type for a slot, that slot is skipped
/// rather than producing a broken widget, and reported back via
/// <see cref="TemplateInstantiationResult.UnfilledSlots"/> — per the v2.0 spec's
/// "prompt the user only for missing fields, never ask unnecessary questions",
/// this is the hook a picker UI would use to ask about exactly (and only) the
/// slots that need help, rather than walking through every slot unconditionally.
/// </summary>
public sealed class TemplateInstantiationService
{
    private readonly ILogger<TemplateInstantiationService> _logger;

    /// <summary>Initialises the template instantiation service with a logger.</summary>
    public TemplateInstantiationService(ILogger<TemplateInstantiationService> logger)
    {
        _logger = logger;
    }

    /// <summary>Builds a new <see cref="Dashboard"/> from a template, reporting any slots that couldn't be auto-filled.</summary>
    public TemplateInstantiationResult InstantiateWithReport(DashboardTemplate template, DataSourceDescriptor dataSource)
    {
        var dashboard = new Dashboard
        {
            Name = template.Name,
            Description = template.Description,
            TemplateName = template.Name
        };

        var measures = dataSource.Fields.Where(f => f.FieldType == FieldType.Measure).ToList();
        var dimensions = dataSource.Fields.Where(f => f.FieldType == FieldType.Dimension).ToList();
        var timeFields = dataSource.Fields.Where(f => f.FieldType == FieldType.Time).ToList();

        var usedMeasures = new HashSet<string>();
        var usedDimensions = new HashSet<string>();
        var unfilled = new List<TemplateWidgetSlot>();

        var col = 0;
        var row = 0;
        var rowMaxHeight = 0;
        const int gridColumns = 24;

        foreach (var slot in template.Slots)
        {
            var slotMeasures = measures.Where(m => !usedMeasures.Contains(m.Name)).Take(slot.MeasuresNeeded).ToList();
            if (slotMeasures.Count < slot.MeasuresNeeded)
            {
                _logger.LogDebug("Skipping slot '{Slot}': not enough measure fields ({Have}/{Need}).",
                    slot.Title, slotMeasures.Count, slot.MeasuresNeeded);
                unfilled.Add(slot);
                continue;
            }

            List<FieldDescriptor> slotDimensions;
            if (slot.PreferTimeDimension && timeFields.Count > 0)
            {
                slotDimensions = timeFields.Take(1).ToList();
            }
            else
            {
                slotDimensions = dimensions.Where(d => !usedDimensions.Contains(d.Name)).Take(slot.DimensionsNeeded).ToList();
                if (slotDimensions.Count < slot.DimensionsNeeded)
                {
                    _logger.LogDebug("Skipping slot '{Slot}': not enough dimension fields.", slot.Title);
                    unfilled.Add(slot);
                    continue;
                }
            }

            foreach (var m in slotMeasures) usedMeasures.Add(m.Name);
            foreach (var d in slotDimensions) usedDimensions.Add(d.Name);

            if (col + slot.ColumnSpan > gridColumns)
            {
                col = 0;
                row += rowMaxHeight;
                rowMaxHeight = 0;
            }

            var widget = new DashboardWidget
            {
                Title = slot.Title,
                VisualType = slot.VisualType,
                DataSourceId = dataSource.Id,
                MeasureFields = slotMeasures.Select(m => m.Name).ToList(),
                DimensionFields = slotDimensions.Select(d => d.Name).ToList(),
                Layout = new WidgetLayout { Column = col, Row = row, ColumnSpan = slot.ColumnSpan, RowSpan = slot.RowSpan }
            };

            dashboard.Widgets.Add(widget);
            col += slot.ColumnSpan;
            rowMaxHeight = Math.Max(rowMaxHeight, slot.RowSpan);
        }

        _logger.LogInformation("Template '{Template}' instantiated: {Filled}/{Total} slots filled from '{Source}'.",
            template.Name, dashboard.Widgets.Count, template.Slots.Count, dataSource.Name);

        return new TemplateInstantiationResult(dashboard, unfilled);
    }

    /// <summary>Convenience overload for callers that don't need the unfilled-slot report.</summary>
    public Dashboard Instantiate(DashboardTemplate template, DataSourceDescriptor dataSource) =>
        InstantiateWithReport(template, dataSource).Dashboard;
}
