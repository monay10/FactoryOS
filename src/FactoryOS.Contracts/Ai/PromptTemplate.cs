namespace FactoryOS.Contracts.Ai;

/// <summary>
/// A named, versioned prompt template. The <see cref="System"/> and <see cref="User"/> bodies may contain
/// <c>{{variable}}</c> placeholders that the prompt engine fills at render time. Prompts are data, not code.
/// </summary>
public sealed record PromptTemplate
{
    /// <summary>The logical template key (for example <c>maintenance.summarize-work-order</c>).</summary>
    public required string Key { get; init; }

    /// <summary>The template version; the catalog resolves the highest version for a key.</summary>
    public int Version { get; init; } = 1;

    /// <summary>An optional system-message template.</summary>
    public string? System { get; init; }

    /// <summary>The user-message template.</summary>
    public required string User { get; init; }
}
