using System.Globalization;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Plugins.Runtime.Configuration;

namespace FactoryOS.Plugins.Runtime.Domain;

/// <summary>
/// One tenant's installation of one plugin: which version it runs, what it may do, how it is configured and
/// what state it is in.
/// <para>
/// The instance is where multi-tenancy actually lives. A definition is shared by every factory; an instance
/// belongs to exactly one, and its <see cref="Tenant"/> is part of its <see cref="Identity"/> rather than a
/// field that happens to be checked — so there is no lookup that can return another tenant's plugin.
/// </para>
/// </summary>
public sealed class PluginInstance
{
    private readonly List<PluginPermission> _granted;

    /// <summary>Initializes a new instance of the <see cref="PluginInstance"/> class.</summary>
    /// <param name="tenant">The tenant that owns it.</param>
    /// <param name="pluginKey">The plugin it installs.</param>
    /// <param name="version">The version installed.</param>
    /// <param name="granted">The permissions the tenant grants the plugin.</param>
    /// <param name="settings">The tenant's settings for the plugin.</param>
    public PluginInstance(
        string tenant,
        string pluginKey,
        PluginVersion version,
        IEnumerable<PluginPermission>? granted = null,
        PluginSettings? settings = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginKey);

        Tenant = tenant;
        PluginKey = pluginKey;
        Version = version;
        _granted = granted is null ? [] : [.. granted];
        Settings = settings ?? new PluginSettings(tenant, pluginKey);
    }

    /// <summary>Gets the tenant that owns the instance.</summary>
    public string Tenant { get; }

    /// <summary>Gets the plugin installed.</summary>
    public string PluginKey { get; }

    /// <summary>Gets the version currently installed.</summary>
    public PluginVersion Version { get; private set; }

    /// <summary>Gets the version an update replaced, which a rollback would return to.</summary>
    public PluginVersion? PreviousVersion { get; private set; }

    /// <summary>Gets the identity the store files the instance under — a tenant can never reach another's.</summary>
    public string Identity => Identify(Tenant, PluginKey);

    /// <summary>Gets the tenant's settings for the plugin.</summary>
    public PluginSettings Settings { get; }

    /// <summary>Gets the permissions the tenant has granted the plugin.</summary>
    public IReadOnlyList<PluginPermission> Granted => _granted;

    /// <summary>Gets the resource quota the instance runs under.</summary>
    public PluginResourceQuota Quota { get; private set; } = PluginResourceQuota.Unlimited;

    /// <summary>Gets the current lifecycle status.</summary>
    public PluginRuntimeStatus Status { get; private set; } = PluginRuntimeStatus.Discovered;

    /// <summary>Gets a value indicating whether an operator has switched the plugin off for this tenant.</summary>
    public bool Enabled { get; private set; } = true;

    /// <summary>Gets why the instance failed, when it has.</summary>
    public string? FailureReason { get; private set; }

    /// <summary>Gets how the instance failed, when it has.</summary>
    public PluginFailureKind FailureKind { get; private set; }

    /// <summary>Gets when the instance last entered <see cref="PluginRuntimeStatus.Running"/>.</summary>
    public DateTimeOffset? StartedUtc { get; private set; }

    /// <summary>Gets a value indicating whether the instance will accept work.</summary>
    public bool CanServe => Enabled && Status == PluginRuntimeStatus.Running;

    /// <summary>Gets a value indicating whether the instance holds a loaded plugin object.</summary>
    public bool IsLoaded => Status is PluginRuntimeStatus.Loaded or PluginRuntimeStatus.Starting
        or PluginRuntimeStatus.Running or PluginRuntimeStatus.Suspended or PluginRuntimeStatus.Stopping
        or PluginRuntimeStatus.Stopped or PluginRuntimeStatus.Updating;

    /// <summary>Gets a value indicating whether a rollback has a version to return to.</summary>
    public bool CanRollback => PreviousVersion is not null;

    /// <summary>Builds the identity a tenant's plugin is filed under.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="pluginKey">The plugin key.</param>
    /// <returns>The identity.</returns>
    public static string Identify(string tenant, string pluginKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginKey);
        return string.Create(CultureInfo.InvariantCulture, $"{tenant}|{pluginKey}");
    }

    /// <summary>
    /// Computes what the plugin may actually do: the intersection of what its manifest asks for and what the
    /// tenant has granted.
    /// <para>
    /// Both halves matter. A plugin can never exceed its own manifest, so what it may do is auditable from
    /// the package alone; and it can never exceed the tenant's grant, so a factory is never surprised by a
    /// permission an update quietly added.
    /// </para>
    /// </summary>
    /// <param name="definition">The definition whose requests bound the result.</param>
    /// <returns>The effective permissions.</returns>
    public IReadOnlyList<PluginPermission> EffectivePermissions(PluginDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return [.. definition.EffectiveRequests()
            .Where(requested => _granted.Any(granted => granted.Grants(requested)))];
    }

    /// <summary>Lists the permissions the plugin asks for that the tenant has not granted.</summary>
    /// <param name="definition">The definition whose requests are checked.</param>
    /// <returns>The ungranted requests.</returns>
    public IReadOnlyList<PluginPermission> UngrantedRequests(PluginDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return [.. definition.EffectiveRequests()
            .Where(requested => !_granted.Any(granted => granted.Grants(requested)))];
    }

    /// <summary>Replaces the permissions the tenant grants the plugin.</summary>
    /// <param name="permissions">The granted permissions.</param>
    public void Grant(IEnumerable<PluginPermission> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        _granted.Clear();
        _granted.AddRange(permissions);
    }

    /// <summary>Narrows the resource quota the instance runs under.</summary>
    /// <param name="quota">The quota.</param>
    public void UseQuota(PluginResourceQuota quota)
    {
        ArgumentNullException.ThrowIfNull(quota);
        Quota = quota;
    }

    /// <summary>Marks the package installed for the tenant.</summary>
    public void MarkInstalled() => Transition(PluginRuntimeStatus.Installed);

    /// <summary>Marks the plugin's assembly loaded and its entry type activated.</summary>
    public void MarkLoaded() => Transition(PluginRuntimeStatus.Loaded);

    /// <summary>Marks the plugin as starting.</summary>
    public void MarkStarting() => Transition(PluginRuntimeStatus.Starting);

    /// <summary>Marks the plugin as running, clearing any recorded failure.</summary>
    /// <param name="startedUtc">When it started.</param>
    public void MarkRunning(DateTimeOffset startedUtc)
    {
        StartedUtc = startedUtc;
        FailureReason = null;
        FailureKind = PluginFailureKind.Unknown;
        Transition(PluginRuntimeStatus.Running);
    }

    /// <summary>Marks the plugin as suspended: still loaded, holding its state, refusing new work.</summary>
    public void MarkSuspended() => Transition(PluginRuntimeStatus.Suspended);

    /// <summary>Marks the plugin as stopping.</summary>
    public void MarkStopping() => Transition(PluginRuntimeStatus.Stopping);

    /// <summary>Marks the plugin as stopped; its instance stays loaded.</summary>
    public void MarkStopped()
    {
        StartedUtc = null;
        Transition(PluginRuntimeStatus.Stopped);
    }

    /// <summary>Marks the plugin as being replaced by another version.</summary>
    public void MarkUpdating() => Transition(PluginRuntimeStatus.Updating);

    /// <summary>Returns the instance to the installed state after its assembly context is released.</summary>
    public void MarkUnloaded()
    {
        StartedUtc = null;
        Transition(PluginRuntimeStatus.Installed);
    }

    /// <summary>Marks the plugin as removed from the tenant.</summary>
    public void MarkRemoved()
    {
        StartedUtc = null;
        Transition(PluginRuntimeStatus.Removed);
    }

    /// <summary>Marks the instance as failed and records why.</summary>
    /// <param name="kind">How it failed.</param>
    /// <param name="reason">Why it failed.</param>
    public void MarkFailed(PluginFailureKind kind, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        FailureKind = kind;
        FailureReason = reason;
        Transition(PluginRuntimeStatus.Failed);
    }

    /// <summary>
    /// Records that the instance now runs a different version, remembering the one replaced so a rollback
    /// has somewhere to go.
    /// </summary>
    /// <param name="version">The version now installed.</param>
    public void MoveTo(PluginVersion version)
    {
        if (version == Version)
        {
            return;
        }

        PreviousVersion = Version;
        Version = version;
    }

    /// <summary>Switches the plugin on for this tenant.</summary>
    public void Enable() => Enabled = true;

    /// <summary>Switches the plugin off; a disabled instance serves nothing whatever its status.</summary>
    public void Disable() => Enabled = false;

    private void Transition(PluginRuntimeStatus status) => Status = status;
}
