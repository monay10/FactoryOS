using FactoryOS.Contracts.Ai;

namespace FactoryOS.Ai.Brain;

/// <summary>The Company Brain's built-in prompt templates. Registered into the catalog so they remain data.</summary>
public static class BrainPrompts
{
    /// <summary>The catalog key of the grounded-answer template.</summary>
    public const string AnswerKey = "company-brain.answer";

    /// <summary>
    /// The default grounded-answer template. It instructs the model to answer strictly from the retrieved
    /// context and to admit when the context is insufficient — the guardrail that keeps RAG honest. It takes
    /// two variables: <c>context</c> (the grounding block) and <c>question</c>.
    /// </summary>
    public static PromptTemplate Answer { get; } = new()
    {
        Key = AnswerKey,
        Version = 1,
        System =
            "You are FactoryOS Company Brain, a factory operations assistant. Answer the question using ONLY " +
            "the numbered context below. If the context does not contain the answer, say you do not have that " +
            "information in the knowledge base. Be concise and factual, and cite sources by their [n] markers.",
        User = "Context:\n{{context}}\n\nQuestion: {{question}}",
    };
}
