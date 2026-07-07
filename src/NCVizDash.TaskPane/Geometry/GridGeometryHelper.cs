using NCVizDash.Models;

namespace NCVizDash.TaskPane.Geometry;

/// <summary>Orientation of a detected alignment guide line.</summary>
public enum GuideOrientation
{
    /// <summary>Vertical alignment guide.</summary>
    Vertical,
    /// <summary>Horizontal alignment guide.</summary>
    Horizontal
}

/// <summary>
/// A single alignment guide: a line at <see cref="GridPosition"/> (in grid units)
/// that the widget currently being dragged/resized is aligned to.
/// </summary>
public readonly record struct AlignmentGuide(GuideOrientation Orientation, int GridPosition);

/// <summary>
/// Pure, WPF-free grid math used by the dashboard canvas: pixel↔grid-unit conversion,
/// snap-to-grid, layout bounds clamping, and alignment-guide detection. Kept free of
/// any UI framework dependency so it can be exercised directly by unit tests.
/// </summary>
public static class GridGeometryHelper
{
    /// <summary>Pixel size of one grid unit. Must match the canvas's visual grid overlay.</summary>
    public const double UnitSize = 40d;

    /// <summary>Minimum widget width, in grid columns.</summary>
    public const int MinColumnSpan = 2;

    /// <summary>Minimum widget height, in grid rows.</summary>
    public const int MinRowSpan = 2;

    /// <summary>Tolerance, in pixels, within which two edges are considered "aligned" for guide purposes.</summary>
    private const double AlignmentTolerancePixels = 6d;

    /// <summary>Converts a pixel offset to the nearest whole grid unit.</summary>
    public static int SnapToGrid(double pixels) => (int)Math.Round(pixels / UnitSize, MidpointRounding.AwayFromZero);

    /// <summary>Converts a grid-unit count to pixels.</summary>
    public static double ToPixels(int gridUnits) => gridUnits * UnitSize;

    /// <summary>
    /// Clamps a proposed new column/row so the widget never moves fully off the
    /// (optionally bounded) grid. Negative positions are clamped to zero; if
    /// <paramref name="gridBound"/> is provided, the widget is also kept from
    /// running past the right edge.
    /// </summary>
    public static int ClampPosition(int proposed, int span, int? gridBound = null)
    {
        var clamped = Math.Max(0, proposed);
        if (gridBound is int bound)
            clamped = Math.Min(clamped, Math.Max(0, bound - span));
        return clamped;
    }

    /// <summary>Clamps a proposed span to the configured minimum (widgets can't be resized to nothing).</summary>
    public static int ClampColumnSpan(int proposed) => Math.Max(MinColumnSpan, proposed);

    /// <summary>Clamps a proposed row span to the configured minimum.</summary>
    public static int ClampRowSpan(int proposed) => Math.Max(MinRowSpan, proposed);

    /// <summary>
    /// Compares the moving widget's current edges against every other widget's edges
    /// and returns a guide for each axis where an edge lines up within tolerance.
    /// At most one vertical and one horizontal guide is returned (the closest match per axis).
    /// </summary>
    public static IReadOnlyList<AlignmentGuide> ComputeAlignmentGuides(
        DashboardWidget moving, IEnumerable<DashboardWidget> others)
    {
        var guides = new List<AlignmentGuide>();

        var movingLeft = moving.Layout.Column;
        var movingRight = moving.Layout.Column + moving.Layout.ColumnSpan;
        var movingTop = moving.Layout.Row;
        var movingBottom = moving.Layout.Row + moving.Layout.RowSpan;

        AlignmentGuide? bestVertical = null;
        AlignmentGuide? bestHorizontal = null;
        var bestVerticalDistance = double.MaxValue;
        var bestHorizontalDistance = double.MaxValue;

        foreach (var other in others)
        {
            if (other.Id == moving.Id) continue;

            var otherLeft = other.Layout.Column;
            var otherRight = other.Layout.Column + other.Layout.ColumnSpan;
            var otherTop = other.Layout.Row;
            var otherBottom = other.Layout.Row + other.Layout.RowSpan;

            foreach (var (mEdge, oEdge) in new[]
                     {
                         (movingLeft, otherLeft), (movingLeft, otherRight),
                         (movingRight, otherLeft), (movingRight, otherRight)
                     })
            {
                var distancePixels = Math.Abs(ToPixels(mEdge) - ToPixels(oEdge));
                if (distancePixels <= AlignmentTolerancePixels && distancePixels < bestVerticalDistance)
                {
                    bestVerticalDistance = distancePixels;
                    bestVertical = new AlignmentGuide(GuideOrientation.Vertical, oEdge);
                }
            }

            foreach (var (mEdge, oEdge) in new[]
                     {
                         (movingTop, otherTop), (movingTop, otherBottom),
                         (movingBottom, otherTop), (movingBottom, otherBottom)
                     })
            {
                var distancePixels = Math.Abs(ToPixels(mEdge) - ToPixels(oEdge));
                if (distancePixels <= AlignmentTolerancePixels && distancePixels < bestHorizontalDistance)
                {
                    bestHorizontalDistance = distancePixels;
                    bestHorizontal = new AlignmentGuide(GuideOrientation.Horizontal, oEdge);
                }
            }
        }

        if (bestVertical is { } v) guides.Add(v);
        if (bestHorizontal is { } h) guides.Add(h);

        return guides;
    }

    /// <summary>
    /// Returns true if the two widgets' grid footprints overlap. Used to decide
    /// whether a proposed move/resize would collide with another widget.
    /// </summary>
    public static bool Overlaps(WidgetLayout a, WidgetLayout b)
    {
        var aLeft = a.Column; var aRight = a.Column + a.ColumnSpan;
        var aTop = a.Row; var aBottom = a.Row + a.RowSpan;
        var bLeft = b.Column; var bRight = b.Column + b.ColumnSpan;
        var bTop = b.Row; var bBottom = b.Row + b.RowSpan;

        return aLeft < bRight && aRight > bLeft && aTop < bBottom && aBottom > bTop;
    }

    /// <summary>
    /// Finds the first grid slot that does not overlap any existing widget,
    /// optionally preferring a drop location (snapped to the grid).
    /// </summary>
    public static (int Column, int Row) FindNextFreeSlot(
        IEnumerable<DashboardWidget> existing,
        int colSpan,
        int rowSpan,
        int gridColumns,
        int? preferColumn = null,
        int? preferRow = null)
    {
        colSpan = ClampColumnSpan(colSpan);
        rowSpan = ClampRowSpan(rowSpan);
        gridColumns = Math.Max(gridColumns, colSpan);

        if (preferColumn is int pc && preferRow is int pr)
        {
            var col = ClampPosition(pc, colSpan, gridColumns);
            var row = Math.Max(0, pr);
            if (!OverlapsAny(existing, col, row, colSpan, rowSpan))
                return (col, row);
        }

        var maxRow = existing.Any()
            ? existing.Max(w => w.Layout.Row + w.Layout.RowSpan)
            : 0;

        for (var row = 0; row <= maxRow + rowSpan + 8; row++)
        {
            for (var col = 0; col <= gridColumns - colSpan; col++)
            {
                if (!OverlapsAny(existing, col, row, colSpan, rowSpan))
                    return (col, row);
            }
        }

        return (0, maxRow + 2);
    }

    private static bool OverlapsAny(
        IEnumerable<DashboardWidget> existing,
        int col, int row, int colSpan, int rowSpan)
    {
        var probe = new WidgetLayout { Column = col, Row = row, ColumnSpan = colSpan, RowSpan = rowSpan };
        return existing.Any(w => Overlaps(w.Layout, probe));
    }
}
