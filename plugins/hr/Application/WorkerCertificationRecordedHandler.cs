using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Hr.Domain;

namespace FactoryOS.Plugins.Hr.Application;

/// <summary>
/// The HR module's consumer of <see cref="WorkerCertificationRecorded"/>. It records the worker's certification
/// and expiry. Recording is last-write-wins, so a redelivery is harmless. It references no other module — only
/// the shared event vocabulary.
/// </summary>
public sealed class WorkerCertificationRecordedHandler : IEventHandler<WorkerCertificationRecorded>
{
    private readonly ICertificationRegistry _registry;

    /// <summary>Initializes a new instance of the <see cref="WorkerCertificationRecordedHandler"/> class.</summary>
    /// <param name="registry">The certification registry.</param>
    public WorkerCertificationRecordedHandler(ICertificationRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
    }

    /// <inheritdoc />
    public Task HandleAsync(
        WorkerCertificationRecorded integrationEvent,
        EventContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        _registry.Record(
            new WorkerKey(integrationEvent.Tenant, integrationEvent.WorkerId),
            integrationEvent.Certification,
            integrationEvent.ExpiresAt);

        return Task.CompletedTask;
    }
}
