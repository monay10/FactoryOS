using FactoryOS.Contracts.Ai;

namespace FactoryOS.Ai.Providers;

/// <summary>Maps the canonical <see cref="ChatRole"/> to the wire role strings both backends share.</summary>
internal static class ChatRoleNames
{
    public static string ToWire(ChatRole role) => role switch
    {
        ChatRole.System => "system",
        ChatRole.User => "user",
        ChatRole.Assistant => "assistant",
        ChatRole.Tool => "tool",
        _ => "user",
    };
}
