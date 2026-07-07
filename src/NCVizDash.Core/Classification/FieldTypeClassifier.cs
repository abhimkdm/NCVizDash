using System.Text.RegularExpressions;
using NCVizDash.Models;

namespace NCVizDash.Core.Classification;

/// <summary>
/// Deterministically classifies a column's CLR type (and optionally its name)
/// into a <see cref="FieldType"/>. No AI — pure type-based rules per spec.
/// </summary>
public static class FieldTypeClassifier
{
    private static readonly string[] BooleanNameHints =
        ["is", "has", "flag", "active", "enabled", "completed", "approved"];

    private static readonly string[] TimeNameHints =
        ["date", "time", "month", "year", "quarter", "day", "created", "modified", "updated", "deadline"];

    private static readonly string[] MeasureNameHints =
        ["amount", "revenue", "cost", "price", "total", "count", "qty", "quantity",
         "hours", "days", "score", "rate", "percent", "value", "sum", "budget"];

    /// <summary>
    /// Classifies a field based on the sample CLR type observed in the column,
    /// falling back to name-based heuristics when the type is ambiguous (e.g. all-null columns).
    /// </summary>
    /// <param name="columnName">Raw column / header name.</param>
    /// <param name="clrType">The dominant CLR type detected for the column's values.</param>
    public static FieldType Classify(string columnName, Type clrType)
    {
        // 1. Boolean → Filter (highest precedence: unambiguous type)
        if (clrType == typeof(bool))
            return FieldType.Filter;

        // 2. DateTime / DateTimeOffset → Time
        if (clrType == typeof(DateTime) || clrType == typeof(DateTimeOffset))
            return FieldType.Time;

        // 3. Numeric → Measure (unless the name strongly suggests an ID/key, which we
        //    still treat as a Dimension since IDs aren't meaningfully summable)
        if (IsNumericType(clrType))
        {
            return LooksLikeIdentifier(columnName)
                ? FieldType.Dimension
                : FieldType.Measure;
        }

        // 4. Text → Dimension, unless name hints say otherwise (defensive fallback
        //    for cases where Excel stored a date/number as text).
        if (clrType == typeof(string))
        {
            var words = SplitIntoWords(columnName);

            if (TimeNameHints.Any(h => words.Contains(h)))
                return FieldType.Time;

            if (BooleanNameHints.Any(h => words.Contains(h)))
                return FieldType.Filter;

            return FieldType.Dimension;
        }

        return FieldType.Unknown;
    }

    /// <summary>
    /// Classifies a field by inspecting a sample of raw values rather than a single CLR type.
    /// Useful when reading loosely-typed Excel ranges where a column may contain a mix
    /// of nulls, numbers, and text.
    /// </summary>
    public static FieldType ClassifyFromSample(string columnName, IEnumerable<object?> sampleValues)
    {
        var nonNull = sampleValues.Where(v => v is not null).ToList();
        if (nonNull.Count == 0)
            return Classify(columnName, typeof(object)); // falls through to name-hint logic

        // Pick the most common runtime type among the sample.
        var dominantType = nonNull
            .GroupBy(v => v!.GetType())
            .OrderByDescending(g => g.Count())
            .First()
            .Key;

        return Classify(columnName, dominantType);
    }

    // ── private helpers ──────────────────────────────────────────────────────

    private static bool IsNumericType(Type type) =>
        type == typeof(byte)   || type == typeof(sbyte)  ||
        type == typeof(short)  || type == typeof(ushort) ||
        type == typeof(int)    || type == typeof(uint)   ||
        type == typeof(long)   || type == typeof(ulong)  ||
        type == typeof(float)  || type == typeof(double) ||
        type == typeof(decimal);

    private static bool LooksLikeIdentifier(string columnName)
    {
        var lower = columnName.ToLowerInvariant().Trim();
        return lower == "id" || lower.EndsWith("id") || lower.EndsWith("_id") ||
               lower.EndsWith("code") || lower.EndsWith("number") || lower.EndsWith("no");
    }

    /// <summary>
    /// Splits a PascalCase/camelCase/snake_case column name into individual lowercase
    /// words, so name-hint matching compares whole words rather than raw substrings.
    /// This is what prevents "IssueType" from matching the "is" boolean hint (it
    /// splits into ["issue", "type"], neither of which equals "is") while still
    /// correctly matching "IsActive" (splits into ["is", "active"]).
    /// </summary>
    private static string[] SplitIntoWords(string name)
    {
        var spaced = Regex.Replace(name, "(?<!^)(?=[A-Z])", " ");
        return spaced.Split(['_', ' ', '-'], StringSplitOptions.RemoveEmptyEntries)
                     .Select(w => w.ToLowerInvariant())
                     .ToArray();
    }
}