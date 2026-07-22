using FactoryOS.Connectors.Runtime.Discovery;
using FactoryOS.Connectors.Runtime.Domain;
using FactoryOS.Connectors.Runtime.Events;
using FactoryOS.Connectors.Runtime.Persistence;
using FactoryOS.Contracts.Connectors;
using FactoryOS.Domain.Abstractions;
using FactoryOS.Domain.Results;

namespace FactoryOS.Connectors.Runtime.Execution;

/// <summary>What one pass of discovery and loading did.</summary>
/// <param name="Loaded">The definitions that were loaded.</param>
/// <param name="Rejected">The candidate folders that yielded nothing, with the reason.</param>
public sealed record ConnectorLoadSummary(
    IReadOnlyList<ConnectorDefinition> Loaded,
    IReadOnlyList<ConnectorDiscoveryResult> Rejected)
{
    /// <summary>Gets how many definitions were loaded.</summary>
    public int LoadedCount => Loaded.Count;

    /// <summary>Gets how many candidates were rejected.</summary>
    public int RejectedCount => Rejected.Count;
}

/// <summary>
/// Brings connector definitions into the runtime: validates them, records them in the repository, attaches
/// the handler that will perform their operations, and announces both steps.
/// <para>
/// Registering and loading are two events on purpose. A definition can be <b>known</b> — catalogued,
/// browsable, configurable — before anything is attached that can actually perform it. Collapsing them would
/// leave an operator unable to tell "we do not ship that connector" from "that connector is not wired up".
/// </para>
/// </summary>
public sealed class ConnectorLoader
{
    private readonly IConnectorRepository _repository;
    private readonly IConnectorDiscovery _discovery;
    private readonly CompatibilityValidator _validator;
    private readonly ConnectorInvoker _invoker;
    private readonly ConnectorRuntimePublisher _events;
    private readonly IDateTimeProvider _clock;

    /// <summary>Initializes a new instance of the <see cref="ConnectorLoader"/> class.</summary>
    /// <param name="repository">The definition repository.</param>
    /// <param name="discovery">Connector discovery.</param>
    /// <param name="validator">The compatibility validator.</param>
    /// <param name="invoker">The invoker handlers are attached to.</param>
    /// <param name="events">The event publisher.</param>
    /// <param name="clock">The clock.</param>
    public ConnectorLoader(
        IConnectorRepository repository,
        IConnectorDiscovery discovery,
        CompatibilityValidator validator,
        ConnectorInvoker invoker,
        ConnectorRuntimePublisher events,
        IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(discovery);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(invoker);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(clock);

        _repository = repository;
        _discovery = discovery;
        _validator = validator;
        _invoker = invoker;
        _events = events;
        _clock = clock;
    }

    /// <summary>Validates a definition and records it in the catalogue.</summary>
    /// <param name="definition">The definition.</param>
    /// <returns>A successful result, or the first incoherence found.</returns>
    public Result Register(ConnectorDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var validation = _validator.Validate(definition);
        if (validation.IsFailure)
        {
            return validation;
        }

        _repository.Register(definition);
        _events.Publish(new ConnectorRegistered(
            definition.Key, definition.Version.ToString(), definition.Category)
        {
            OccurredUtc = _clock.UtcNow,
        });

        return Result.Success();
    }

    /// <summary>Registers a definition and attaches the handler that performs its operations.</summary>
    /// <param name="definition">The definition.</param>
    /// <param name="handler">The handler.</param>
    /// <returns>A successful result, or a failure explaining why it could not be loaded.</returns>
    public Result Load(ConnectorDefinition definition, IConnectorOperationHandler handler)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(handler);

        if (!string.Equals(handler.ConnectorKey, definition.Key, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(Error.Validation(
                "Connector.Load.KeyMismatch",
                $"The handler serves connector '{handler.ConnectorKey}' but definition '{definition.Key}' was "
                + "being loaded."));
        }

        foreach (var operation in definition.Operations)
        {
            if (!handler.CanHandle(operation.Name))
            {
                return Result.Failure(Error.Validation(
                    "Connector.Load.UnhandledOperation",
                    $"Connector '{definition.Key}' declares operation '{operation.Name}', which its handler does "
                    + "not perform."));
            }
        }

        var registration = Register(definition);
        if (registration.IsFailure)
        {
            return registration;
        }

        _invoker.Attach(handler);
        _events.Publish(new ConnectorLoaded(definition.Key, definition.Operations.Count)
        {
            OccurredUtc = _clock.UtcNow,
        });

        return Result.Success();
    }

    /// <summary>
    /// Loads an existing inbound connector, deriving its definition from the connector manifest and adapting
    /// it to the runtime — the path by which every connector already in the repository becomes invocable
    /// without being touched.
    /// </summary>
    /// <param name="connector">The connector.</param>
    /// <param name="manifest">Its manifest.</param>
    /// <param name="category">The kind of external system it speaks to.</param>
    /// <returns>A successful result, or a failure explaining why it could not be loaded.</returns>
    public Result LoadInbound(
        IConnector connector, ConnectorManifest manifest, ConnectorCategory category = ConnectorCategory.Unknown)
    {
        ArgumentNullException.ThrowIfNull(connector);
        ArgumentNullException.ThrowIfNull(manifest);

        var definition = ConnectorDefinition.FromManifest(
            manifest, new Framework.Runtime.ConnectorVersion(1, 0, 0), Framework.Runtime.ConnectorCapability.Read,
            category);

        return Load(definition, new InboundConnectorOperationHandler(connector));
    }

    /// <summary>Loads an existing outbound connector, adapting it to the runtime's delivery operation.</summary>
    /// <param name="connector">The outbound connector.</param>
    /// <param name="manifest">Its manifest.</param>
    /// <param name="category">The kind of external system it speaks to.</param>
    /// <returns>A successful result, or a failure explaining why it could not be loaded.</returns>
    public Result LoadOutbound(
        IOutboundConnector connector,
        ConnectorManifest manifest,
        ConnectorCategory category = ConnectorCategory.Unknown)
    {
        ArgumentNullException.ThrowIfNull(connector);
        ArgumentNullException.ThrowIfNull(manifest);

        var definition = ConnectorDefinition.FromManifest(
            manifest, new Framework.Runtime.ConnectorVersion(1, 0, 0), Framework.Runtime.ConnectorCapability.Write,
            category);

        return Load(definition, new OutboundConnectorOperationHandler(connector));
    }

    /// <summary>
    /// Scans a directory and registers every valid manifest it finds. Definitions are catalogued, not
    /// attached: discovery answers "what connectors does this deployment ship?", and a handler is attached
    /// when the connector's assembly is actually wired in.
    /// </summary>
    /// <param name="rootDirectory">The directory whose child folders each hold one connector.</param>
    /// <returns>What the pass loaded and what it rejected, with reasons.</returns>
    public ConnectorLoadSummary DiscoverAndRegister(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        var loaded = new List<ConnectorDefinition>();
        var rejected = new List<ConnectorDiscoveryResult>();

        foreach (var found in _discovery.Discover(rootDirectory))
        {
            if (found.Definition is null)
            {
                rejected.Add(found);
                continue;
            }

            var registration = Register(found.Definition);
            if (registration.IsSuccess)
            {
                loaded.Add(found.Definition);
            }
            else
            {
                rejected.Add(found with { Definition = null, Error = registration.Error });
            }
        }

        return new ConnectorLoadSummary(loaded, rejected);
    }

    /// <summary>Removes a definition and detaches its handler.</summary>
    /// <param name="key">The definition key.</param>
    /// <returns><see langword="true"/> when a definition was removed.</returns>
    public bool Unload(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _invoker.Detach(key);
        return _repository.Remove(key);
    }
}
