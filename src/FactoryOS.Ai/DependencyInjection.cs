using FactoryOS.Ai.Agents;
using FactoryOS.Ai.Brain;
using FactoryOS.Ai.Configuration;
using FactoryOS.Ai.Gateway;
using FactoryOS.Ai.Knowledge;
using FactoryOS.Ai.Prompts;
using FactoryOS.Ai.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration entry point for the FactoryOS <b>LLM Gateway</b> — the single, vendor-agnostic door to
/// language models. Providers speak HTTP to their backends; callers only ever see logical model keys.
/// </summary>
public static class AiServiceCollectionExtensions
{
    /// <summary>Registers the LLM Gateway, its providers and their configuration.</summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <param name="configuration">The configuration to bind routing and provider options from.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddLlmGateway(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<LlmGatewayOptions>(configuration.GetSection(LlmGatewayOptions.SectionName));
        services.Configure<OpenAiProviderOptions>(configuration.GetSection(OpenAiProviderOptions.SectionName));
        services.Configure<OllamaProviderOptions>(configuration.GetSection(OllamaProviderOptions.SectionName));

        services.AddHttpClient<OpenAiCompatibleProvider>(static (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<OpenAiProviderOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(options.BaseUrl))
            {
                client.BaseAddress = new Uri(options.BaseUrl);
            }
        });

        services.AddHttpClient<OllamaProvider>(static (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<OllamaProviderOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(options.BaseUrl))
            {
                client.BaseAddress = new Uri(options.BaseUrl);
            }
        });

        services.AddTransient<ILlmProvider>(static sp => sp.GetRequiredService<OpenAiCompatibleProvider>());
        services.AddTransient<ILlmProvider>(static sp => sp.GetRequiredService<OllamaProvider>());
        services.AddTransient<ILlmGateway, LlmGateway>();

        return services;
    }

    /// <summary>Registers the Embedding Gateway, its providers and their routing configuration.</summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <param name="configuration">The configuration to bind routing and provider options from.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddEmbeddingGateway(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<EmbeddingGatewayOptions>(configuration.GetSection(EmbeddingGatewayOptions.SectionName));
        services.Configure<OpenAiProviderOptions>(configuration.GetSection(OpenAiProviderOptions.SectionName));
        services.Configure<OllamaProviderOptions>(configuration.GetSection(OllamaProviderOptions.SectionName));

        services.AddHttpClient<OpenAiEmbeddingProvider>(static (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<OpenAiProviderOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(options.BaseUrl))
            {
                client.BaseAddress = new Uri(options.BaseUrl);
            }
        });

        services.AddHttpClient<OllamaEmbeddingProvider>(static (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<OllamaProviderOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(options.BaseUrl))
            {
                client.BaseAddress = new Uri(options.BaseUrl);
            }
        });

        services.AddTransient<IEmbeddingProvider>(static sp => sp.GetRequiredService<OpenAiEmbeddingProvider>());
        services.AddTransient<IEmbeddingProvider>(static sp => sp.GetRequiredService<OllamaEmbeddingProvider>());
        services.AddTransient<IEmbeddingGateway, EmbeddingGateway>();

        return services;
    }

    /// <summary>Registers the knowledge base — the tenant-scoped vector store, indexer and retriever (RAG).</summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddKnowledgeBase(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IKnowledgeStore, InMemoryKnowledgeStore>();
        services.TryAddTransient<IKnowledgeIndexer, KnowledgeIndexer>();
        services.TryAddTransient<IKnowledgeRetriever, KnowledgeRetriever>();

        return services;
    }

    /// <summary>
    /// Registers the Company Brain — the tenant-aware grounded Q&amp;A facade over retrieval, the prompt engine
    /// and the LLM Gateway. Also ensures its dependencies (knowledge base, prompt engine) and seeds the
    /// built-in grounded-answer template into the catalog.
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddCompanyBrain(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddKnowledgeBase();
        services.AddPromptEngine();

        services.AddTransient<ICompanyBrain>(static sp =>
        {
            // Seed the built-in template (idempotent: the catalog keeps the highest version).
            sp.GetRequiredService<IPromptCatalog>().Register(BrainPrompts.Answer);
            return new CompanyBrain(
                sp.GetRequiredService<IKnowledgeRetriever>(),
                sp.GetRequiredService<IPromptComposer>(),
                sp.GetRequiredService<ILlmGateway>());
        });

        return services;
    }

    /// <summary>
    /// Registers the Agent Framework — the shared agent runtime and manifest catalog. Agents are data
    /// (manifests) resolved through the catalog; the single runtime executes any of them. Ensures the knowledge
    /// base and prompt engine it depends on.
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddAgentFramework(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddKnowledgeBase();
        services.AddPromptEngine();
        services.TryAddSingleton<IAgentCatalog, InMemoryAgentCatalog>();
        services.TryAddTransient<IAgentRuntime, AgentRuntime>();

        return services;
    }

    /// <summary>Registers the prompt engine — the strict renderer, template catalog and message composer.</summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddPromptEngine(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IPromptRenderer, PromptRenderer>();
        services.TryAddSingleton<IPromptCatalog, InMemoryPromptCatalog>();
        services.TryAddSingleton<IPromptComposer, PromptComposer>();

        return services;
    }
}
