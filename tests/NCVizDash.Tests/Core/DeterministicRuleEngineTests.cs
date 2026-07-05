using Microsoft.Extensions.Logging.Abstractions;
using NCVizDash.Models;
using NCVizDash.RuleEngine;
using Xunit;

namespace NCVizDash.Tests.Core;

/// <summary>Unit tests for <see cref="DeterministicRuleEngine"/>.</summary>
public sealed class DeterministicRuleEngineTests
{
    private readonly DeterministicRuleEngine _sut = new(NullLogger<DeterministicRuleEngine>.Instance);

    [Fact]
    public void Recommend_SingleMeasure_ReturnsKpi()
    {
        var fields = new[]
        {
            new FieldDescriptor { Name = "Revenue", FieldType = FieldType.Measure }
        };

        var result = _sut.Recommend(fields);

        Assert.Equal(VisualType.Kpi, result);
    }

    [Fact]
    public void Recommend_MeasurePlusDimension_ReturnsBar()
    {
        var fields = new[]
        {
            new FieldDescriptor { Name = "Revenue",    FieldType = FieldType.Measure   },
            new FieldDescriptor { Name = "Department", FieldType = FieldType.Dimension }
        };

        var result = _sut.Recommend(fields);

        Assert.Equal(VisualType.Bar, result);
    }

    [Fact]
    public void Recommend_MeasurePlusTime_ReturnsLine()
    {
        var fields = new[]
        {
            new FieldDescriptor { Name = "Revenue", FieldType = FieldType.Measure },
            new FieldDescriptor { Name = "Month",   FieldType = FieldType.Time    }
        };

        var result = _sut.Recommend(fields);

        Assert.Equal(VisualType.Line, result);
    }

    [Fact]
    public void Recommend_MeasurePlusTwoDimensions_ReturnsPie()
    {
        var fields = new[]
        {
            new FieldDescriptor { Name = "Cost",       FieldType = FieldType.Measure   },
            new FieldDescriptor { Name = "Category",   FieldType = FieldType.Dimension },
            new FieldDescriptor { Name = "SubCategory",FieldType = FieldType.Dimension }
        };

        var result = _sut.Recommend(fields);

        Assert.Equal(VisualType.Pie, result);
    }

    [Fact]
    public void Recommend_MultipleMeasures_ReturnsBar()
    {
        var fields = new[]
        {
            new FieldDescriptor { Name = "Revenue", FieldType = FieldType.Measure },
            new FieldDescriptor { Name = "Cost",    FieldType = FieldType.Measure }
        };

        var result = _sut.Recommend(fields);

        Assert.Equal(VisualType.Bar, result);
    }

    [Fact]
    public void Recommend_EmptyFields_ReturnsTable()
    {
        var result = _sut.Recommend(Array.Empty<FieldDescriptor>());

        Assert.Equal(VisualType.Table, result);
    }
}
