namespace FactoryOS.Plugins.RuleEngine.Domain;

/// <summary>
/// The pure decision at the heart of the Rule Engine: does an observed value stand in a rule's comparison
/// relation to its threshold? No tenant, no clock, no I/O — just a total function over an operator, a value and
/// a threshold, so the whole rule vocabulary is exhaustively testable.
/// </summary>
public static class RuleEvaluator
{
    /// <summary>Evaluates a single comparison.</summary>
    /// <param name="op">The comparison to apply.</param>
    /// <param name="value">The observed value.</param>
    /// <param name="threshold">The threshold to compare against.</param>
    /// <returns><see langword="true"/> when the value matches the comparison.</returns>
    public static bool Matches(ComparisonOperator op, decimal value, decimal threshold) => op switch
    {
        ComparisonOperator.GreaterThan => value > threshold,
        ComparisonOperator.GreaterOrEqual => value >= threshold,
        ComparisonOperator.LessThan => value < threshold,
        ComparisonOperator.LessOrEqual => value <= threshold,
        ComparisonOperator.Equal => value == threshold,
        ComparisonOperator.NotEqual => value != threshold,
        _ => throw new ArgumentOutOfRangeException(nameof(op), op, "Unknown comparison operator."),
    };
}
