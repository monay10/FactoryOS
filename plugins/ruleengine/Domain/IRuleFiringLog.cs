namespace FactoryOS.Plugins.RuleEngine.Domain;

/// <summary>
/// Records which (rule, reading) pairs have already fired, so at-least-once delivery does not re-emit a match.
/// The dedupe key is the pair, not the event id alone, because one reading may match several rules — each rule
/// must fire exactly once per reading. The realization of the "idempotent consumers deduplicate by event id"
/// invariant, widened to the rule that consumed the event.
/// </summary>
public interface IRuleFiringLog
{
    /// <summary>Atomically marks a rule as fired for a reading, reporting whether this was the first time.</summary>
    /// <param name="ruleId">The rule that matched.</param>
    /// <param name="sourceEventId">The reading event's stable identifier.</param>
    /// <returns><see langword="true"/> if newly marked; <see langword="false"/> if already fired.</returns>
    bool TryMarkFired(string ruleId, Guid sourceEventId);
}
