using FactoryOS.Connectors.Runtime.Discovery;
using FactoryOS.Connectors.Runtime.Domain;
using FactoryOS.Connectors.Runtime.Events;
using FactoryOS.Connectors.Runtime.Persistence;
using FactoryOS.Connectors.Runtime.Security;
using FactoryOS.Domain.Abstractions;
using FactoryOS.Domain.Results;

namespace FactoryOS.Connectors.Runtime.Execution;

/// <summary>
/// Drives connector instances through their lifecycle and dispatches invocations to them.
/// <para>
/// Starting an instance is where every promise it makes is checked while somebody is still watching: the
/// definition it names must be loaded, the version it pins must be satisfied, a handler must be attached, and
/// its credential must resolve. All four are things that would otherwise surface as a confusing failure on a
/// shift, hours later, to somebody who did not configure it.
/// </para>
/// </summary>
public sealed class ConnectorRuntime
{
    private readonly ConnectorInstanceRegistry _registry;
    private readonly IConnectorRepository _definitions;
    private readonly IConnectorStore _instances;
    private readonly ConnectorDispatcher _dispatcher;
    private readonly ConnectorInvoker _invoker;
    private readonly CompatibilityValidator _validator;
    private readonly ConnectorSecretResolver _secrets;
    private readonly ConnectorSessionManager _sessions;
    private readonly ConnectorRuntimePublisher _events;
    private readonly IDateTimeProvider _clock;

    /// <summary>Initializes a new instance of the <see cref="ConnectorRuntime"/> class.</summary>
    /// <param name="registry">The instance registry.</param>
    /// <param name="definitions">The definition repository.</param>
    /// <param name="instances">The instance store.</param>
    /// <param name="dispatcher">The dispatcher.</param>
    /// <param name="invoker">The invoker, consulted for an attached handler.</param>
    /// <param name="validator">The compatibility validator.</param>
    /// <param name="secrets">The secret resolver.</param>
    /// <param name="sessions">The session manager.</param>
    /// <param name="events">The event publisher.</param>
    /// <param name="clock">The clock.</param>
    public ConnectorRuntime(
        ConnectorInstanceRegistry registry,
        IConnectorRepository definitions,
        IConnectorStore instances,
        ConnectorDispatcher dispatcher,
        ConnectorInvoker invoker,
        CompatibilityValidator validator,
        ConnectorSecretResolver secrets,
        ConnectorSessionManager sessions,
        ConnectorRuntimePublisher events,
        IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(instances);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(invoker);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(secrets);
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(clock);

        _registry = registry;
        _definitions = definitions;
        _instances = instances;
        _dispatcher = dispatcher;
        _invoker = invoker;
        _validator = validator;
        _secrets = secrets;
        _sessions = sessions;
        _events = events;
        _clock = clock;
    }

    /// <summary>Starts a tenant's instance so it will accept invocations.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="key">The instance key.</param>
    /// <returns>A successful result, or a failure explaining what stopped it — with the instance left faulted.</returns>
    public Result Start(string tenant, string key)
    {
        var instance = _instances.Find(tenant, key);
        if (instance is null)
        {
            return Result.Failure(Error.NotFound(
                "Connector.Runtime.NotFound", $"Tenant '{tenant}' has no connector instance '{key}'."));
        }

        instance.MarkStarting();

        var definition = _definitions.Find(instance.DefinitionKey);
        if (definition is null)
        {
            return Fault(instance, Error.NotFound(
                "Connector.Runtime.NoDefinition",
                $"Connector '{instance.DefinitionKey}' is not registered."));
        }

        var compatibility = _validator.ValidateInstance(definition, instance);
        if (compatibility.IsFailure)
        {
            return Fault(instance, compatibility.Error);
        }

        if (_invoker.Find(definition.Key) is null)
        {
            return Fault(instance, Error.Validation(
                "Connector.Runtime.NoHandler",
                $"Connector '{definition.Key}' is registered but nothing is attached that can perform its "
                + "operations."));
        }

        var credential = _secrets.ResolveFor(instance);
        if (!credential.IsComplete)
        {
            return Fault(instance, Error.Validation(
                "Connector.Runtime.UnresolvedSecret",
                $"The secret credential '{credential.Key}' refers to could not be resolved."));
        }

        instance.MarkRunning();
        _instances.Save(instance);

        _events.Publish(new ConnectorStarted(instance.Key, definition.Key)
        {
            Tenant = tenant,
            OccurredUtc = _clock.UtcNow,
        });

        return Result.Success();
    }

    /// <summary>Stops a tenant's instance and closes its session.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="key">The instance key.</param>
    /// <param name="reason">Why it is being stopped.</param>
    /// <returns>A successful result, or a failure when the tenant has no such instance.</returns>
    public Result Stop(string tenant, string key, string reason = "requested")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var instance = _instances.Find(tenant, key);
        if (instance is null)
        {
            return Result.Failure(Error.NotFound(
                "Connector.Runtime.NotFound", $"Tenant '{tenant}' has no connector instance '{key}'."));
        }

        instance.MarkStopping();
        _sessions.Close(tenant, key);
        instance.MarkStopped();
        _instances.Save(instance);

        _events.Publish(new ConnectorStopped(instance.Key, reason)
        {
            Tenant = tenant,
            OccurredUtc = _clock.UtcNow,
        });

        return Result.Success();
    }

    /// <summary>Stops and starts an instance, so a configuration change takes effect.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="key">The instance key.</param>
    /// <returns>A successful result, or the failure that stopped it.</returns>
    public Result Restart(string tenant, string key)
    {
        var stop = Stop(tenant, key, "restart");
        return stop.IsFailure ? stop : Start(tenant, key);
    }

    /// <summary>Invokes an operation.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">A token to cancel the invocation.</param>
    /// <returns>The response.</returns>
    public Task<ConnectorResponse> InvokeAsync(
        ConnectorRequest request, CancellationToken cancellationToken = default) =>
        _dispatcher.DispatchAsync(request, cancellationToken);

    /// <summary>Gets the instance registry.</summary>
    public ConnectorInstanceRegistry Instances => _registry;

    private Result Fault(ConnectorInstance instance, Error error)
    {
        instance.MarkFaulted(error.Description);
        _instances.Save(instance);
        return Result.Failure(error);
    }
}
