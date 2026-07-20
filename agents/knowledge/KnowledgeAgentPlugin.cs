using FactoryOS.Agents.Knowledge.Application;
using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FactoryOS.Agents.Knowledge;

/// <summary>
/// The Knowledge Ingest agent — the bridge that gives the Company Brain a memory of live operations. It subscribes
/// to the noteworthy events other modules raise and, through the Knowledge Indexer (embed → store), writes each as
/// a retrievable, citable document. It depends only on the indexer abstraction (resolved from the host) and the
/// shared events — never a module, never an in-process model. Installing or removing this folder adds or removes
/// the memory with zero core changes.
/// </summary>
/// <remarks>
/// The host must have registered an <c>IKnowledgeIndexer</c> and an embedding gateway (via <c>AddKnowledge</c> /
/// <c>AddEmbeddingGateway</c>); the agent consumes them.
/// </remarks>
public sealed class KnowledgeAgentPlugin : PluginBase
{
    /// <summary>The plugin key, matching <c>agent.json</c>.</summary>
    public const string PluginKey = "agent.knowledge";

    /// <inheritdoc />
    public override string Key => PluginKey;

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(new KnowledgeIngestOptions());
        services.TryAddSingleton<KnowledgeIngestor>();

        services.AddScoped<IEventHandler<RuleTriggered>, RuleTriggeredHandler>();
        services.AddScoped<IEventHandler<WorkOrderCreated>, WorkOrderCreatedHandler>();
        services.AddScoped<IEventHandler<SafetyStandDownTriggered>, SafetyStandDownTriggeredHandler>();
        services.AddScoped<IEventHandler<QualityAlertRaised>, QualityAlertRaisedHandler>();
        services.AddScoped<IEventHandler<ProductionOrderCompleted>, ProductionOrderCompletedHandler>();
        services.AddScoped<IEventHandler<DeliveryHealthDegraded>, DeliveryHealthDegradedHandler>();
        services.AddScoped<IEventHandler<EnergySpikeDetected>, EnergySpikeDetectedHandler>();
        services.AddScoped<IEventHandler<LowStockDetected>, LowStockDetectedHandler>();
        services.AddScoped<IEventHandler<CertificationGapDetected>, CertificationGapDetectedHandler>();
        services.AddScoped<IEventHandler<InsightGenerated>, InsightGeneratedHandler>();
    }
}
