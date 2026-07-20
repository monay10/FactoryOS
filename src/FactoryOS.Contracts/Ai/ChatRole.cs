namespace FactoryOS.Contracts.Ai;

/// <summary>The author role of a chat message, in the canonical FactoryOS AI model.</summary>
public enum ChatRole
{
    /// <summary>System / developer instructions that steer the assistant.</summary>
    System,

    /// <summary>An end-user message.</summary>
    User,

    /// <summary>A message produced by the assistant.</summary>
    Assistant,

    /// <summary>The result of a tool/function call fed back to the assistant.</summary>
    Tool,
}
