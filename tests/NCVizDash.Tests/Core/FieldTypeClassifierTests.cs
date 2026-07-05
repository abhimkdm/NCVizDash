using NCVizDash.Core.Classification;
using NCVizDash.Models;
using Xunit;

namespace NCVizDash.Tests.Core;

/// <summary>Unit tests for <see cref="FieldTypeClassifier"/>.</summary>
public sealed class FieldTypeClassifierTests
{
    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(double))]
    [InlineData(typeof(decimal))]
    [InlineData(typeof(long))]
    public void Classify_NumericType_ReturnsMeasure(Type clrType)
    {
        var result = FieldTypeClassifier.Classify("Revenue", clrType);
        Assert.Equal(FieldType.Measure, result);
    }

    [Fact]
    public void Classify_NumericIdColumn_ReturnsDimension()
    {
        var result = FieldTypeClassifier.Classify("EmployeeId", typeof(int));
        Assert.Equal(FieldType.Dimension, result);
    }

    [Fact]
    public void Classify_NumericCodeColumn_ReturnsDimension()
    {
        var result = FieldTypeClassifier.Classify("ProjectCode", typeof(int));
        Assert.Equal(FieldType.Dimension, result);
    }

    [Fact]
    public void Classify_DateTimeType_ReturnsTime()
    {
        var result = FieldTypeClassifier.Classify("OrderDate", typeof(DateTime));
        Assert.Equal(FieldType.Time, result);
    }

    [Fact]
    public void Classify_DateTimeOffsetType_ReturnsTime()
    {
        var result = FieldTypeClassifier.Classify("CreatedAt", typeof(DateTimeOffset));
        Assert.Equal(FieldType.Time, result);
    }

    [Fact]
    public void Classify_BooleanType_ReturnsFilter()
    {
        var result = FieldTypeClassifier.Classify("IsActive", typeof(bool));
        Assert.Equal(FieldType.Filter, result);
    }

    [Fact]
    public void Classify_PlainStringType_ReturnsDimension()
    {
        var result = FieldTypeClassifier.Classify("Department", typeof(string));
        Assert.Equal(FieldType.Dimension, result);
    }

    [Fact]
    public void Classify_StringWithDateHint_ReturnsTime()
    {
        var result = FieldTypeClassifier.Classify("ModifiedDate", typeof(string));
        Assert.Equal(FieldType.Time, result);
    }

    [Fact]
    public void Classify_StringWithBooleanHint_ReturnsFilter()
    {
        var result = FieldTypeClassifier.Classify("is_completed", typeof(string));
        Assert.Equal(FieldType.Filter, result);
    }

    [Fact]
    public void ClassifyFromSample_AllNumericValues_ReturnsMeasure()
    {
        var sample = new object?[] { 100.0, 200.0, 150.5, null };
        var result = FieldTypeClassifier.ClassifyFromSample("Cost", sample);
        Assert.Equal(FieldType.Measure, result);
    }

    [Fact]
    public void ClassifyFromSample_MixedDominantString_ReturnsDimension()
    {
        var sample = new object?[] { "Engineering", "Sales", "Engineering", 5.0 };
        var result = FieldTypeClassifier.ClassifyFromSample("Department", sample);
        Assert.Equal(FieldType.Dimension, result);
    }

    [Fact]
    public void ClassifyFromSample_AllNull_FallsBackToNameHints()
    {
        var sample = new object?[] { null, null, null };
        var result = FieldTypeClassifier.ClassifyFromSample("LaunchDate", sample);
        Assert.Equal(FieldType.Time, result);
    }

    [Fact]
    public void ClassifyFromSample_BooleanDominant_ReturnsFilter()
    {
        var sample = new object?[] { true, false, true };
        var result = FieldTypeClassifier.ClassifyFromSample("Approved", sample);
        Assert.Equal(FieldType.Filter, result);
    }

    [Fact]
    public void ClassifyFromSample_DateTimeDominant_ReturnsTime()
    {
        var sample = new object?[] { DateTime.Now, DateTime.Now.AddDays(-1) };
        var result = FieldTypeClassifier.ClassifyFromSample("Timestamp", sample);
        Assert.Equal(FieldType.Time, result);
    }
}
