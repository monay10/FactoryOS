namespace FactoryOS.Plugins.RuleEngine.Domain;

/// <summary>
/// The comparison a rule applies between an observed value and its threshold. It is bound from configuration by
/// name, so the set of comparators a factory may use is fixed by the platform while the rules themselves stay data.
/// </summary>
public enum ComparisonOperator
{
    /// <summary>value &gt; threshold.</summary>
    GreaterThan,

    /// <summary>value &gt;= threshold.</summary>
    GreaterOrEqual,

    /// <summary>value &lt; threshold.</summary>
    LessThan,

    /// <summary>value &lt;= threshold.</summary>
    LessOrEqual,

    /// <summary>value == threshold.</summary>
    Equal,

    /// <summary>value != threshold.</summary>
    NotEqual,
}
