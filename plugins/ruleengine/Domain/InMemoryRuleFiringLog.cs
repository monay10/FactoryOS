using System.Collections.Concurrent;

namespace FactoryOS.Plugins.RuleEngine.Domain;

/// <summary>The default in-memory <see cref="IRuleFiringLog"/>, backed by a concurrent set of (rule, event) pairs.</summary>
public sealed class InMemoryRuleFiringLog : IRuleFiringLog
{
    private readonly ConcurrentDictionary<(string RuleId, Guid SourceEventId), byte> _fired = new();

    /// <inheritdoc />
    public bool TryMarkFired(string ruleId, Guid sourceEventId) => _fired.TryAdd((ruleId, sourceEventId), 0);
}
