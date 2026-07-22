using FactoryOS.Connectors.Framework.Runtime;
using FactoryOS.Contracts.Connectors;

namespace FactoryOS.Connectors.Runtime.Domain;

/// <summary>
/// One named thing a connector can be asked to do. An operation is the unit the runtime invokes, meters,
/// authorizes and caches — the connector contract itself only exposes a record stream, which is one operation
/// among many once a connector can also write, execute a command or move a file.
/// </summary>
/// <param name="Name">The operation name, unique within its definition (for example <c>read</c>).</param>
/// <param name="Capability">The capability the operation exercises; the definition must declare it.</param>
/// <param name="Permission">The permission a caller must hold.</param>
public sealed record ConnectorOperation(
    string Name,
    ConnectorCapability Capability,
    ConnectorPermission Permission)
{
    /// <summary>Gets a human-readable description.</summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets a value indicating whether repeating the operation has the same effect as performing it once.
    /// Only an idempotent operation is ever retried: retrying a non-idempotent write is how one purchase
    /// order becomes three.
    /// </summary>
    public bool Idempotent { get; init; }

    /// <summary>Gets a value indicating whether successful responses may be reused from the cache.</summary>
    public bool Cacheable { get; init; }

    /// <summary>Gets the parameter names a request must supply.</summary>
    public IReadOnlyList<string> RequiredParameters { get; init; } = [];

    /// <summary>Gets the deadline for a single attempt, or <see langword="null"/> to use the runtime default.</summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>Gets the resilience this operation narrows to, or <see langword="null"/> to inherit.</summary>
    public ConnectorResiliencePolicy? Resilience { get; init; }

    /// <summary>Builds the conventional read operation every inbound connector supports.</summary>
    /// <returns>The operation.</returns>
    public static ConnectorOperation Read() => new(
        Configuration.ConnectorRuntimeConstants.ReadOperation,
        ConnectorCapability.Read,
        ConnectorPermissions.Read)
    {
        Description = "Streams raw source records for the requesting tenant.",
        Idempotent = true,
    };

    /// <summary>Builds the conventional delivery operation every outbound connector supports.</summary>
    /// <returns>The operation.</returns>
    public static ConnectorOperation Deliver() => new(
        Configuration.ConnectorRuntimeConstants.DeliverOperation,
        ConnectorCapability.Write,
        ConnectorPermissions.Write)
    {
        Description = "Delivers one normalized message to the outside world.",
        Idempotent = false,
    };
}

/// <summary>
/// The runtime's description of a connector <b>kind</b>: what it is, which version of it this is, what it can
/// do and which operations it offers. One definition serves every tenant; a
/// <see cref="ConnectorInstance"/> is one tenant's configured activation of it.
/// <para>
/// This is the declarative half the connector framework's <see cref="ConnectorDescriptor"/> deliberately left
/// out — the descriptor tracks a live registration's mutable state, whereas a definition is immutable data
/// read from a manifest.
/// </para>
/// </summary>
public sealed record ConnectorDefinition
{
    /// <summary>Gets the stable key identifying the connector kind; it matches the connector manifest's key.</summary>
    public required string Key { get; init; }

    /// <summary>Gets the human-readable name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the version of this definition.</summary>
    public required ConnectorVersion Version { get; init; }

    /// <summary>Gets the kind of external system the connector speaks to.</summary>
    public ConnectorCategory Category { get; init; } = ConnectorCategory.Unknown;

    /// <summary>Gets the family the category belongs to.</summary>
    public ConnectorType Type => ConnectorCategories.TypeOf(Category);

    /// <summary>Gets the source system name, as the connector contract reports it.</summary>
    public required string SourceSystem { get; init; }

    /// <summary>Gets an optional description.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the capabilities the connector declares.</summary>
    public ConnectorCapability Capabilities { get; init; } = ConnectorCapability.Read;

    /// <summary>Gets the operations the connector offers.</summary>
    public IReadOnlyList<ConnectorOperation> Operations { get; init; } = [];

    /// <summary>Gets the Standard Model entity types the connector produces.</summary>
    public IReadOnlyList<string> Provides { get; init; } = [];

    /// <summary>Gets the resilience every operation inherits unless it narrows it.</summary>
    public ConnectorResiliencePolicy Resilience { get; init; } = ConnectorResiliencePolicy.Default;

    /// <summary>Gets the directory the definition was discovered in, if any.</summary>
    public string? Location { get; init; }

    /// <summary>Finds an operation by name.</summary>
    /// <param name="name">The operation name.</param>
    /// <returns>The operation, or <see langword="null"/> when the definition offers no such operation.</returns>
    public ConnectorOperation? FindOperation(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        foreach (var operation in Operations)
        {
            if (string.Equals(operation.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return operation;
            }
        }

        return null;
    }

    /// <summary>Determines whether the definition declares a capability.</summary>
    /// <param name="capability">The capability to test for.</param>
    /// <returns><see langword="true"/> when the capability is declared.</returns>
    public bool Supports(ConnectorCapability capability) => Capabilities.Supports(capability);

    /// <summary>Gets the resilience an operation runs under, narrowed from the definition's own.</summary>
    /// <param name="operation">The operation.</param>
    /// <returns>The operation's resilience.</returns>
    public ConnectorResiliencePolicy ResilienceFor(ConnectorOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return operation.Resilience ?? Resilience;
    }

    /// <summary>Builds a definition from a connector manifest, defaulting everything the manifest omits.</summary>
    /// <param name="manifest">The connector manifest.</param>
    /// <param name="version">The connector version.</param>
    /// <param name="capabilities">The declared capabilities.</param>
    /// <param name="category">The category of external system.</param>
    /// <param name="location">The directory it was discovered in, if any.</param>
    /// <returns>The definition, carrying the conventional operations its capabilities imply.</returns>
    public static ConnectorDefinition FromManifest(
        ConnectorManifest manifest,
        ConnectorVersion version,
        ConnectorCapability capabilities,
        ConnectorCategory category = ConnectorCategory.Unknown,
        string? location = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var operations = new List<ConnectorOperation>();
        if (capabilities.Supports(ConnectorCapability.Read))
        {
            operations.Add(ConnectorOperation.Read());
        }

        if (capabilities.Supports(ConnectorCapability.Write))
        {
            operations.Add(ConnectorOperation.Deliver());
        }

        return new ConnectorDefinition
        {
            Key = manifest.Key,
            Name = manifest.Name,
            Version = version == default ? new ConnectorVersion(1, 0, 0) : version,
            Category = category,
            SourceSystem = manifest.SourceSystem,
            Description = manifest.Description,
            Capabilities = capabilities,
            Operations = operations,
            Provides = manifest.Provides,
            Location = location,
        };
    }
}
