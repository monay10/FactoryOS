using System.Collections.Concurrent;
using FactoryOS.Connectors.Runtime.Domain;
using FactoryOS.Connectors.Runtime.Events;
using FactoryOS.Connectors.Runtime.Persistence;
using FactoryOS.Connectors.Runtime.Security;
using FactoryOS.Domain.Abstractions;

namespace FactoryOS.Connectors.Runtime.Execution;

/// <summary>
/// Turns a request into an invocation and runs it through the pipeline.
/// <para>
/// Everything a request cannot be allowed to influence is resolved here, before the first stage runs: which
/// instance it reaches, which definition that instance activates, which operation it names and what
/// resilience the call runs under. A middleware that could still change any of those would make the
/// authorization decision taken further in a statement about something else.
/// </para>
/// <para>
/// The instance lookup is tenant-qualified. Reaching another factory's connector is not refused by a check
/// that could be removed — there is no lookup that would find it.
/// </para>
/// </summary>
public sealed class ConnectorDispatcher
{
    private readonly ConcurrentDictionary<string, bool> _lastOutcome = new(StringComparer.Ordinal);
    private readonly IConnectorRepository _definitions;
    private readonly IConnectorStore _instances;
    private readonly ConnectorPipeline _pipeline;
    private readonly ConnectorInvoker _invoker;
    private readonly ConnectorRuntimePublisher _events;
    private readonly IDateTimeProvider _clock;

    /// <summary>Initializes a new instance of the <see cref="ConnectorDispatcher"/> class.</summary>
    /// <param name="definitions">The definition repository.</param>
    /// <param name="instances">The instance store.</param>
    /// <param name="pipeline">The invocation pipeline.</param>
    /// <param name="invoker">The terminal invoker.</param>
    /// <param name="events">The event publisher.</param>
    /// <param name="clock">The clock.</param>
    public ConnectorDispatcher(
        IConnectorRepository definitions,
        IConnectorStore instances,
        ConnectorPipeline pipeline,
        ConnectorInvoker invoker,
        ConnectorRuntimePublisher events,
        IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(instances);
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(invoker);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(clock);

        _definitions = definitions;
        _instances = instances;
        _pipeline = pipeline;
        _invoker = invoker;
        _events = events;
        _clock = clock;
    }

    /// <summary>Dispatches a request.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">A token to cancel the invocation.</param>
    /// <returns>The response; a request that cannot be resolved fails as a value, not an exception.</returns>
    public async Task<ConnectorResponse> DispatchAsync(
        ConnectorRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var instance = _instances.Find(request.Tenant, request.Instance);
        if (instance is null)
        {
            return ConnectorResponse.Failed(ConnectorError.NotFound(
                "Connector.Dispatch.NoInstance",
                $"Tenant '{request.Tenant}' has no connector instance '{request.Instance}'."));
        }

        var definition = _definitions.Find(instance.DefinitionKey);
        if (definition is null)
        {
            return ConnectorResponse.Failed(ConnectorError.NotFound(
                "Connector.Dispatch.NoDefinition",
                $"Connector instance '{instance.Key}' activates '{instance.DefinitionKey}', which is not loaded."));
        }

        var operation = definition.FindOperation(request.Operation);
        if (operation is null)
        {
            return ConnectorResponse.Failed(ConnectorError.NotFound(
                "Connector.Dispatch.NoOperation",
                $"Connector '{definition.Key}' offers no operation '{request.Operation}'."));
        }

        var invocation = new ConnectorInvocation(
            request, instance, definition, operation, ResilienceFor(definition, instance, operation), _clock.UtcNow);

        var response = await _pipeline
            .ExecuteAsync(invocation, _invoker.InvokeAsync, cancellationToken)
            .ConfigureAwait(false);

        Announce(invocation, response);
        return response;
    }

    /// <summary>
    /// Narrows the resilience an invocation runs under. An operation's own policy wins over its instance's,
    /// which wins over its definition's — the more specific a policy is, the more it knows.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="instance">The instance.</param>
    /// <param name="operation">The operation.</param>
    /// <returns>The resilience.</returns>
    public static ConnectorResiliencePolicy ResilienceFor(
        ConnectorDefinition definition, ConnectorInstance instance, ConnectorOperation operation)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(operation);

        return operation.Resilience ?? instance.Resilience ?? definition.Resilience;
    }

    private void Announce(ConnectorInvocation invocation, ConnectorResponse response)
    {
        var telemetry = invocation.Telemetry(response);
        var key = $"{invocation.Tenant}|{invocation.Instance.Key}|{invocation.Operation.Name}";
        var previouslySucceeded = !_lastOutcome.TryGetValue(key, out var last) || last;
        _lastOutcome[key] = response.Succeeded;

        _events.Publish(new ConnectorInvoked(telemetry)
        {
            Tenant = invocation.Tenant,
            OccurredUtc = _clock.UtcNow,
            Correlation = invocation.Correlation,
        });

        if (!response.Succeeded && response.Error is { } error)
        {
            _events.Publish(new ConnectorFailed(
                invocation.Instance.Key, invocation.Operation.Name, error, telemetry.Attempts)
            {
                Tenant = invocation.Tenant,
                OccurredUtc = _clock.UtcNow,
                Correlation = invocation.Correlation,
            });

            return;
        }

        if (response.Succeeded && !previouslySucceeded)
        {
            _events.Publish(new ConnectorRecovered(invocation.Instance.Key, invocation.Operation.Name)
            {
                Tenant = invocation.Tenant,
                OccurredUtc = _clock.UtcNow,
                Correlation = invocation.Correlation,
            });
        }
    }
}
