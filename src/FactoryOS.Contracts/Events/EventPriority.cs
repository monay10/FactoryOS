namespace FactoryOS.Contracts.Events;

/// <summary>Relative importance of an event, propagated as message metadata for prioritising transports.</summary>
public enum EventPriority
{
    /// <summary>Low importance; may be processed after higher-priority work.</summary>
    Low = 0,

    /// <summary>The default importance for ordinary events.</summary>
    Normal = 1,

    /// <summary>High importance; should be favoured over normal work.</summary>
    High = 2,

    /// <summary>Critical importance; should be processed ahead of all other events.</summary>
    Critical = 3,
}
