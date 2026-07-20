namespace FactoryOS.Contracts.Ai;

/// <summary>A single message in a chat conversation.</summary>
/// <param name="Role">The author role.</param>
/// <param name="Content">The message text.</param>
public sealed record ChatMessage(ChatRole Role, string Content);
