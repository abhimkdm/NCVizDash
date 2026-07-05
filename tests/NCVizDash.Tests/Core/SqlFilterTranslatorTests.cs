using NCVizDash.Core.Analytics;
using NCVizDash.Models;
using Xunit;

namespace NCVizDash.Tests.Core;

/// <summary>Unit tests for <see cref="SqlFilterTranslator"/>.</summary>
public sealed class SqlFilterTranslatorTests
{
    [Fact]
    public void BuildClauses_EqualsOperator_ProducesEqualityClause()
    {
        var clauses = SqlFilterTranslator.BuildClauses(
            [new WidgetFilter { FieldName = "region", Operator = FilterOperator.Equals, Values = ["EMEA"] }]);

        Assert.Single(clauses);
        Assert.Equal("\"region\" = 'EMEA'", clauses[0]);
    }

    [Fact]
    public void BuildClauses_DisabledFilter_Excluded()
    {
        var clauses = SqlFilterTranslator.BuildClauses(
            [new WidgetFilter { FieldName = "region", Operator = FilterOperator.Equals, Values = ["EMEA"], IsEnabled = false }]);

        Assert.Empty(clauses);
    }

    [Fact]
    public void BuildWhereFragment_JoinsWithAnd_NoLeadingKeyword()
    {
        var fragment = SqlFilterTranslator.BuildWhereFragment(
        [
            new WidgetFilter { FieldName = "region", Operator = FilterOperator.Equals, Values = ["EMEA"] },
            new WidgetFilter { FieldName = "dept", Operator = FilterOperator.Equals, Values = ["Eng"] }
        ]);

        Assert.Equal("\"region\" = 'EMEA' AND \"dept\" = 'Eng'", fragment);
    }

    [Fact]
    public void BuildWhereFragment_NoFilters_ReturnsEmptyString()
    {
        Assert.Equal(string.Empty, SqlFilterTranslator.BuildWhereFragment([]));
    }

    [Fact]
    public void SanitiseColumnName_SpacesAndSymbols_ReplacedWithUnderscore()
    {
        Assert.Equal("sub_category_", SqlFilterTranslator.SanitiseColumnName("Sub Category!"));
    }

    [Fact]
    public void LiteralOf_NumericString_ReturnedUnquoted()
    {
        Assert.Equal("1000", SqlFilterTranslator.LiteralOf("1000"));
    }

    [Fact]
    public void LiteralOf_TextValue_QuotedAndEscaped()
    {
        Assert.Equal("'O''Brien'", SqlFilterTranslator.LiteralOf("O'Brien"));
    }

    [Fact]
    public void LiteralOf_Null_ReturnsNullLiteral()
    {
        Assert.Equal("NULL", SqlFilterTranslator.LiteralOf(null));
    }
}
