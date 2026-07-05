using System.Text.Json.Serialization;

namespace NCVizDash.Models;

/// <summary>Classifies a field for automatic rule-engine matching.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FieldType
{
    /// <summary>Numeric measure (SUM, AVG, etc.).</summary>
    Measure,

    /// <summary>Text or categorical dimension (GROUP BY).</summary>
    Dimension,

    /// <summary>Date / DateTime field for time-series.</summary>
    Time,

    /// <summary>Boolean field used for filters.</summary>
    Filter,

    /// <summary>Unknown or unclassified.</summary>
    Unknown
}

/// <summary>Describes a single column/field from a data source.</summary>
public sealed class FieldDescriptor
{
    /// <summary>Column name as it appears in the source.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Display-friendly alias shown in the UI.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>CLR type string (e.g. "System.Decimal").</summary>
    public string ClrType { get; init; } = string.Empty;

    /// <summary>Automatically classified field role.</summary>
    public FieldType FieldType { get; set; } = FieldType.Unknown;

    /// <summary>Whether this field is currently visible in the explorer.</summary>
    public bool IsVisible { get; set; } = true;

    /// <inheritdoc/>
    public override string ToString() => $"{Name} [{FieldType}]";
}

/// <summary>Represents a data source (Excel table, named range, worksheet).</summary>
public sealed class DataSourceDescriptor
{
    /// <summary>Unique identifier for this data source.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Display name shown in the workbook explorer.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Origin of the data (ExcelTable, NamedRange, Worksheet, External).</summary>
    public string SourceType { get; init; } = string.Empty;

    /// <summary>Worksheet name where the data lives.</summary>
    public string SheetName { get; init; } = string.Empty;

    /// <summary>All fields belonging to this data source.</summary>
    public List<FieldDescriptor> Fields { get; init; } = [];

    /// <summary>Row count last seen during load.</summary>
    public long RowCount { get; set; }

    /// <summary>UTC timestamp of the last successful load.</summary>
    public DateTimeOffset LastLoaded { get; set; }
}
