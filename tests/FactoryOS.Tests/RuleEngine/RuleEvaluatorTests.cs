using FactoryOS.Plugins.RuleEngine.Domain;

namespace FactoryOS.Tests.RuleEngine;

public sealed class RuleEvaluatorTests
{
    [Theory]
    [InlineData(ComparisonOperator.GreaterThan, 86, 85, true)]
    [InlineData(ComparisonOperator.GreaterThan, 85, 85, false)]
    [InlineData(ComparisonOperator.GreaterOrEqual, 85, 85, true)]
    [InlineData(ComparisonOperator.GreaterOrEqual, 84, 85, false)]
    [InlineData(ComparisonOperator.LessThan, 1, 2, true)]
    [InlineData(ComparisonOperator.LessThan, 2, 2, false)]
    [InlineData(ComparisonOperator.LessOrEqual, 2, 2, true)]
    [InlineData(ComparisonOperator.LessOrEqual, 3, 2, false)]
    [InlineData(ComparisonOperator.Equal, 2, 2, true)]
    [InlineData(ComparisonOperator.Equal, 2, 3, false)]
    [InlineData(ComparisonOperator.NotEqual, 2, 3, true)]
    [InlineData(ComparisonOperator.NotEqual, 2, 2, false)]
    public void Matches_evaluates_the_comparison(ComparisonOperator op, decimal value, decimal threshold, bool expected) =>
        Assert.Equal(expected, RuleEvaluator.Matches(op, value, threshold));

    [Fact]
    public void An_unknown_operator_throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => RuleEvaluator.Matches((ComparisonOperator)999, 1, 1));
}
