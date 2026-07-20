using FactoryOS.Contracts.Ai;

namespace FactoryOS.Ai.Prompts;

/// <summary>A registry of prompt templates, resolving the highest registered version for a key.</summary>
public interface IPromptCatalog
{
    /// <summary>Registers (or replaces) a template. The highest version for a key wins at lookup.</summary>
    /// <param name="template">The template to register.</param>
    void Register(PromptTemplate template);

    /// <summary>Resolves the current (highest-version) template for a key.</summary>
    /// <param name="key">The logical template key.</param>
    /// <param name="template">The resolved template when found.</param>
    /// <returns><see langword="true"/> when a template is registered for the key; otherwise <see langword="false"/>.</returns>
    bool TryGet(string key, out PromptTemplate template);
}
