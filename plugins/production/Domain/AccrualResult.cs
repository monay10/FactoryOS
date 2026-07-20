namespace FactoryOS.Plugins.Production.Domain;

/// <summary>The outcome of accruing an increment against an order.</summary>
/// <param name="Found">Whether the order was known (released). Counts for unknown orders are ignored.</param>
/// <param name="JustCompleted">Whether this accrual is the one that carried the order to its target for the first time.</param>
/// <param name="ProductId">The order's product (meaningful only when <paramref name="Found"/>).</param>
/// <param name="TargetQuantity">The order's target quantity.</param>
/// <param name="TotalProduced">The running total after this accrual.</param>
public readonly record struct AccrualResult(
    bool Found,
    bool JustCompleted,
    string ProductId,
    int TargetQuantity,
    int TotalProduced)
{
    /// <summary>An accrual against an order that was never released.</summary>
    public static AccrualResult NotFound { get; } = new(false, false, "", 0, 0);
}
