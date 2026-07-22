using System.Globalization;
using FactoryOS.Connectors.Framework.Runtime;

namespace FactoryOS.Connectors.Runtime.Domain;

/// <summary>
/// Where a connector instance reaches its external system. The address is opaque to the runtime — a URL, a
/// host and port, a file path, a broker name — because interpreting it is the connector's job and the moment
/// the core parsed it, the core would know a vendor's dialect.
/// </summary>
/// <param name="Address">The address, in whatever form the connector understands.</param>
public sealed record ConnectorEndpoint(string Address)
{
    /// <summary>Gets an optional scheme or protocol hint (for example <c>https</c>, <c>opc.tcp</c>).</summary>
    public string? Scheme { get; init; }

    /// <summary>Gets the deadline for reaching this endpoint, or <see langword="null"/> to use the default.</summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>Gets a value indicating whether the address is set.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Address);

    /// <inheritdoc />
    public override string ToString() => Scheme is null ? Address : $"{Scheme}://{Address}";
}

/// <summary>
/// One tenant's configured activation of a connector definition: which definition, at which endpoint, with
/// which credential and settings, and what state it is in.
/// <para>
/// The instance is where multi-tenancy actually lives. A definition is shared by every factory; an instance
/// belongs to exactly one, and its <see cref="Tenant"/> is part of its identity rather than a field that
/// happens to be checked — so there is no lookup that can return another tenant's instance.
/// </para>
/// </summary>
public sealed class ConnectorInstance
{
    private readonly Dictionary<string, string?> _settings;

    /// <summary>Initializes a new instance of the <see cref="ConnectorInstance"/> class.</summary>
    /// <param name="tenant">The tenant that owns it.</param>
    /// <param name="key">The instance key, unique within the tenant.</param>
    /// <param name="definitionKey">The definition it activates.</param>
    /// <param name="endpoint">Where it reaches its external system.</param>
    /// <param name="credential">The credential it presents, as a reference.</param>
    /// <param name="settings">The instance's own settings.</param>
    public ConnectorInstance(
        string tenant,
        string key,
        string definitionKey,
        ConnectorEndpoint endpoint,
        ConnectorCredential? credential = null,
        IReadOnlyDictionary<string, string?>? settings = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionKey);
        ArgumentNullException.ThrowIfNull(endpoint);

        Tenant = tenant;
        Key = key;
        DefinitionKey = definitionKey;
        Endpoint = endpoint;
        Credential = credential ?? ConnectorCredential.None;
        _settings = settings is null
            ? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string?>(settings, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Gets the tenant that owns the instance.</summary>
    public string Tenant { get; }

    /// <summary>Gets the instance key, unique within its tenant.</summary>
    public string Key { get; }

    /// <summary>Gets the definition the instance activates.</summary>
    public string DefinitionKey { get; }

    /// <summary>Gets the identity the store files the instance under — a tenant can never reach another's.</summary>
    public string Identity => Identify(Tenant, Key);

    /// <summary>Gets where the instance reaches its external system.</summary>
    public ConnectorEndpoint Endpoint { get; private set; }

    /// <summary>Gets the credential reference the instance presents.</summary>
    public ConnectorCredential Credential { get; private set; }

    /// <summary>Gets the instance's own settings.</summary>
    public IReadOnlyDictionary<string, string?> Settings => _settings;

    /// <summary>Gets the current lifecycle status.</summary>
    public ConnectorStatus Status { get; private set; } = ConnectorStatus.Stopped;

    /// <summary>Gets a value indicating whether an operator has switched the instance off.</summary>
    public bool Enabled { get; private set; } = true;

    /// <summary>Gets the resilience this instance narrows to, or <see langword="null"/> to inherit the definition's.</summary>
    public ConnectorResiliencePolicy? Resilience { get; private set; }

    /// <summary>Gets the minimum definition version the instance requires, if it pins one.</summary>
    public ConnectorVersion? MinimumVersion { get; private set; }

    /// <summary>Gets why the instance faulted, when it has.</summary>
    public string? FailureReason { get; private set; }

    /// <summary>Gets a value indicating whether the instance will accept an invocation.</summary>
    public bool CanInvoke => Enabled && Status is ConnectorStatus.Running or ConnectorStatus.Degraded;

    /// <summary>Builds the identity a tenant's instance is filed under.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="key">The instance key.</param>
    /// <returns>The identity.</returns>
    public static string Identify(string tenant, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return string.Create(CultureInfo.InvariantCulture, $"{tenant}|{key}");
    }

    /// <summary>Reads a setting.</summary>
    /// <param name="name">The setting name.</param>
    /// <returns>The value, or <see langword="null"/> when absent.</returns>
    public string? Setting(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _settings.TryGetValue(name, out var value) ? value : null;
    }

    /// <summary>Replaces the instance's endpoint, credential reference and settings.</summary>
    /// <param name="endpoint">The new endpoint.</param>
    /// <param name="credential">The new credential reference.</param>
    /// <param name="settings">The new settings.</param>
    public void Reconfigure(
        ConnectorEndpoint endpoint,
        ConnectorCredential? credential = null,
        IReadOnlyDictionary<string, string?>? settings = null)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        Endpoint = endpoint;
        Credential = credential ?? Credential;

        if (settings is not null)
        {
            _settings.Clear();
            foreach (var pair in settings)
            {
                _settings[pair.Key] = pair.Value;
            }
        }
    }

    /// <summary>Narrows the resilience this instance invokes under.</summary>
    /// <param name="resilience">The resilience, or <see langword="null"/> to inherit the definition's.</param>
    public void UseResilience(ConnectorResiliencePolicy? resilience) => Resilience = resilience;

    /// <summary>Pins the minimum definition version the instance will run against.</summary>
    /// <param name="version">The minimum version, or <see langword="null"/> to accept any.</param>
    public void RequireVersion(ConnectorVersion? version) => MinimumVersion = version;

    /// <summary>Marks the instance as starting.</summary>
    public void MarkStarting() => Status = ConnectorStatus.Starting;

    /// <summary>Marks the instance as running and clears any recorded failure.</summary>
    public void MarkRunning()
    {
        Status = ConnectorStatus.Running;
        FailureReason = null;
    }

    /// <summary>Marks the instance as running but impaired.</summary>
    /// <param name="reason">What is wrong.</param>
    public void MarkDegraded(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Status = ConnectorStatus.Degraded;
        FailureReason = reason;
    }

    /// <summary>Marks the instance as stopping.</summary>
    public void MarkStopping() => Status = ConnectorStatus.Stopping;

    /// <summary>Marks the instance as stopped.</summary>
    public void MarkStopped() => Status = ConnectorStatus.Stopped;

    /// <summary>Marks the instance as faulted and records why.</summary>
    /// <param name="reason">Why it faulted.</param>
    public void MarkFaulted(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Status = ConnectorStatus.Faulted;
        FailureReason = reason;
    }

    /// <summary>Switches the instance on so the host will start it.</summary>
    public void Enable() => Enabled = true;

    /// <summary>Switches the instance off; a disabled instance refuses invocations whatever its status.</summary>
    public void Disable() => Enabled = false;
}
