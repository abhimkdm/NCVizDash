using NCVizDash.Models;
using NCVizDash.TaskPane.Geometry;
using Xunit;

namespace NCVizDash.Tests.Core;

/// <summary>Unit tests for <see cref="GridGeometryHelper"/>.</summary>
public sealed class GridGeometryHelperTests
{
    // ── SnapToGrid ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0d,   0)]
    [InlineData(20d,  1)]   // exactly half → rounds up
    [InlineData(39d,  1)]   // just under one unit
    [InlineData(40d,  1)]   // exactly one unit
    [InlineData(41d,  1)]   // just over one unit
    [InlineData(60d,  2)]   // 1.5 units → rounds up
    [InlineData(80d,  2)]   // exactly two units
    [InlineData(119d, 3)]
    public void SnapToGrid_ReturnsNearestUnit(double pixels, int expected)
    {
        Assert.Equal(expected, GridGeometryHelper.SnapToGrid(pixels));
    }

    // ── ToPixels ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0,  0d)]
    [InlineData(1, 40d)]
    [InlineData(5, 200d)]
    public void ToPixels_MultipliesByUnitSize(int units, double expected)
    {
        Assert.Equal(expected, GridGeometryHelper.ToPixels(units));
    }

    // ── ClampPosition ────────────────────────────────────────────────────

    [Fact]
    public void ClampPosition_NegativeProposed_ClampsToZero()
    {
        Assert.Equal(0, GridGeometryHelper.ClampPosition(-5, 4));
    }

    [Fact]
    public void ClampPosition_WithBound_PreventsRunningOffRightEdge()
    {
        // Column 20, span 6, grid 24 → max valid column = 24 - 6 = 18
        Assert.Equal(18, GridGeometryHelper.ClampPosition(20, 6, 24));
    }

    [Fact]
    public void ClampPosition_WithinBounds_Unchanged()
    {
        Assert.Equal(5, GridGeometryHelper.ClampPosition(5, 4, 24));
    }

    [Fact]
    public void ClampPosition_NoBound_AllowsLargeValues()
    {
        Assert.Equal(100, GridGeometryHelper.ClampPosition(100, 4));
    }

    // ── ClampSpan ────────────────────────────────────────────────────────

    [Fact]
    public void ClampColumnSpan_BelowMinimum_ClampsToMinimum()
    {
        Assert.Equal(GridGeometryHelper.MinColumnSpan, GridGeometryHelper.ClampColumnSpan(0));
    }

    [Fact]
    public void ClampColumnSpan_AboveMinimum_Unchanged()
    {
        Assert.Equal(8, GridGeometryHelper.ClampColumnSpan(8));
    }

    [Fact]
    public void ClampRowSpan_BelowMinimum_ClampsToMinimum()
    {
        Assert.Equal(GridGeometryHelper.MinRowSpan, GridGeometryHelper.ClampRowSpan(1));
    }

    // ── Overlaps ─────────────────────────────────────────────────────────

    [Fact]
    public void Overlaps_ClearlyOverlapping_ReturnsTrue()
    {
        var a = new WidgetLayout { Column = 0, Row = 0, ColumnSpan = 6, RowSpan = 4 };
        var b = new WidgetLayout { Column = 3, Row = 2, ColumnSpan = 6, RowSpan = 4 };
        Assert.True(GridGeometryHelper.Overlaps(a, b));
    }

    [Fact]
    public void Overlaps_AdjacentHorizontally_ReturnsFalse()
    {
        var a = new WidgetLayout { Column = 0, Row = 0, ColumnSpan = 6, RowSpan = 4 };
        var b = new WidgetLayout { Column = 6, Row = 0, ColumnSpan = 6, RowSpan = 4 };
        Assert.False(GridGeometryHelper.Overlaps(a, b));
    }

    [Fact]
    public void Overlaps_AdjacentVertically_ReturnsFalse()
    {
        var a = new WidgetLayout { Column = 0, Row = 0, ColumnSpan = 6, RowSpan = 4 };
        var b = new WidgetLayout { Column = 0, Row = 4, ColumnSpan = 6, RowSpan = 4 };
        Assert.False(GridGeometryHelper.Overlaps(a, b));
    }

    [Fact]
    public void Overlaps_Identical_ReturnsTrue()
    {
        var a = new WidgetLayout { Column = 2, Row = 2, ColumnSpan = 4, RowSpan = 3 };
        var b = new WidgetLayout { Column = 2, Row = 2, ColumnSpan = 4, RowSpan = 3 };
        Assert.True(GridGeometryHelper.Overlaps(a, b));
    }

    // ── ComputeAlignmentGuides ───────────────────────────────────────────

    [Fact]
    public void ComputeAlignmentGuides_AlignedLeftEdge_ReturnsVerticalGuide()
    {
        var moving = new DashboardWidget
        {
            Layout = new WidgetLayout { Column = 0, Row = 0, ColumnSpan = 6, RowSpan = 4 }
        };
        var stationary = new DashboardWidget
        {
            Layout = new WidgetLayout { Column = 0, Row = 5, ColumnSpan = 6, RowSpan = 4 }
        };

        var guides = GridGeometryHelper.ComputeAlignmentGuides(moving, [stationary]);

        Assert.Contains(guides, g => g.Orientation == GuideOrientation.Vertical && g.GridPosition == 0);
    }

    [Fact]
    public void ComputeAlignmentGuides_AlignedTopEdge_ReturnsHorizontalGuide()
    {
        var moving = new DashboardWidget
        {
            Layout = new WidgetLayout { Column = 0, Row = 0, ColumnSpan = 6, RowSpan = 4 }
        };
        var stationary = new DashboardWidget
        {
            Layout = new WidgetLayout { Column = 8, Row = 0, ColumnSpan = 4, RowSpan = 3 }
        };

        var guides = GridGeometryHelper.ComputeAlignmentGuides(moving, [stationary]);

        Assert.Contains(guides, g => g.Orientation == GuideOrientation.Horizontal && g.GridPosition == 0);
    }

    [Fact]
    public void ComputeAlignmentGuides_SameWidget_Ignored()
    {
        var moving = new DashboardWidget
        {
            Layout = new WidgetLayout { Column = 0, Row = 0, ColumnSpan = 6, RowSpan = 4 }
        };

        var guides = GridGeometryHelper.ComputeAlignmentGuides(moving, [moving]);

        Assert.Empty(guides);
    }

    [Fact]
    public void ComputeAlignmentGuides_FarApart_ReturnsNoGuides()
    {
        var moving = new DashboardWidget
        {
            Layout = new WidgetLayout { Column = 0, Row = 0, ColumnSpan = 4, RowSpan = 3 }
        };
        var stationary = new DashboardWidget
        {
            Layout = new WidgetLayout { Column = 20, Row = 20, ColumnSpan = 4, RowSpan = 3 }
        };

        var guides = GridGeometryHelper.ComputeAlignmentGuides(moving, [stationary]);

        Assert.Empty(guides);
    }
}
