using FactoryOS.Plugins.Workflow.Monitoring.Domain;
using FactoryOS.Plugins.Workflow.Monitoring.Persistence;

namespace FactoryOS.Plugins.Workflow.Monitoring.Collections;

/// <summary>
/// Every collection the platform ships, in one place.
/// <para>
/// The catalogue is the same for every tenant. A metric means what it means — <c>tasks.completed</c> counts
/// completed human tasks whether the factory is in Dudullu or anywhere else — and tenants differ only in the
/// values they produce. That is Law 1 as it applies to measurement: there is no branch here on who is being
/// measured, and there never will be.
/// </para>
/// </summary>
public static class MetricCatalog
{
    /// <summary>Gets every definition in every collection.</summary>
    public static IReadOnlyList<MetricDefinition> All { get; } =
    [
        .. WorkflowMetricCollection.Definitions,
        .. FormsMetricCollection.Definitions,
        .. HumanTaskMetricCollection.Definitions,
        .. ApprovalMetricCollection.Definitions,
        .. NotificationMetricCollection.Definitions,
        .. SlaMetricCollection.Definitions,
        .. AuditMetricCollection.Definitions,
        .. ConnectorMetricCollection.Definitions,
        .. PluginMetricCollection.Definitions,
        .. ApiMetricCollection.Definitions,
        .. InfrastructureMetricCollection.Definitions,
        .. RuntimeMetricCollection.Definitions,
        .. PerformanceMetricCollection.Definitions,
    ];

    /// <summary>Gets the definitions of one collection.</summary>
    /// <param name="category">The collection.</param>
    /// <returns>Its definitions.</returns>
    public static IReadOnlyList<MetricDefinition> For(MetricCategory category) =>
        All.Where(definition => definition.Category == category).ToArray();

    /// <summary>Registers every definition into a repository.</summary>
    /// <param name="repository">The repository.</param>
    public static void RegisterAll(IMetricRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        foreach (var definition in All)
        {
            repository.Register(definition);
        }
    }
}
