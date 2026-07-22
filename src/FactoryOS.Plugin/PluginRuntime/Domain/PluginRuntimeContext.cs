using System.Globalization;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Plugin.Configuration;
using FactoryOS.Plugin.Runtime;

namespace FactoryOS.Plugins.Runtime.Domain;

/// <summary>
/// The context one tenant's plugin runs in: its manifest and configuration, plus the things only a
/// tenant-scoped runtime can tell it — which factory it is serving, what it is actually permitted to do, and
/// which directory is its own.
/// <para>
/// It implements the framework's <see cref="IPluginContext"/> deliberately. A plugin written against the
/// framework keeps working unchanged and simply receives a context that knows more; a plugin that needs the
/// tenant asks for this type. Nothing had to be added to the contract to make that true.
/// </para>
/// </summary>
public sealed class PluginRuntimeContext : IPluginContext
{
    private readonly IReadOnlyList<PluginPermission> _permissions;

    /// <summary>Initializes a new instance of the <see cref="PluginRuntimeContext"/> class.</summary>
    /// <param name="instance">The tenant's installation.</param>
    /// <param name="definition">The definition it installs.</param>
    /// <param name="manifest">The manifest the definition was read from.</param>
    /// <param name="storagePath">The instance's own storage directory.</param>
    public PluginRuntimeContext(
        PluginInstance instance, PluginDefinition definition, PluginManifest manifest, string storagePath)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(storagePath);

        Instance = instance;
        Definition = definition;
        Manifest = manifest;
        StoragePath = storagePath;
        _permissions = instance.EffectivePermissions(definition);
        Configuration = new PluginConfiguration(instance.PluginKey, instance.Enabled, instance.Settings.Values);
    }

    /// <inheritdoc />
    public string Key => Instance.PluginKey;

    /// <summary>Gets the tenant the plugin is serving.</summary>
    public string Tenant => Instance.Tenant;

    /// <summary>Gets the tenant's installation.</summary>
    public PluginInstance Instance { get; }

    /// <summary>Gets the definition installed.</summary>
    public PluginDefinition Definition { get; }

    /// <inheritdoc />
    public PluginManifest Manifest { get; }

    /// <inheritdoc />
    public string? Location => Definition.Location;

    /// <inheritdoc />
    public PluginConfiguration Configuration { get; }

    /// <summary>Gets the directory that belongs to this instance alone.</summary>
    public string StoragePath { get; }

    /// <summary>Gets what the plugin is actually permitted to do here — requests intersected with grants.</summary>
    public IReadOnlyList<PluginPermission> Permissions => _permissions;

    /// <summary>Determines whether the plugin holds a permission in this tenant.</summary>
    /// <param name="permission">The permission being asked for.</param>
    /// <returns><see langword="true"/> when the effective set covers it.</returns>
    public bool Holds(PluginPermission permission) =>
        _permissions.Any(held => held.Grants(permission));

    /// <inheritdoc />
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Tenant}|{Key}@{Definition.Version}");
}

/// <summary>
/// The caller asking the runtime to do something: which factory they are acting in, who they are, and what
/// they hold. Every lifecycle transition names one.
/// </summary>
/// <param name="Tenant">The tenant the caller is acting in.</param>
/// <param name="Subject">Who the caller is.</param>
public sealed record PluginCaller(string Tenant, string Subject)
{
    /// <summary>Gets the permissions the caller arrived holding.</summary>
    public IReadOnlyList<PluginPermission> Permissions { get; init; } = [];

    /// <summary>Gets a value indicating whether the caller was authenticated.</summary>
    public bool IsAuthenticated { get; init; } = true;

    /// <summary>Builds a caller holding a set of permissions.</summary>
    /// <param name="tenant">The tenant the caller is acting in.</param>
    /// <param name="subject">Who the caller is.</param>
    /// <param name="permissions">What they hold.</param>
    /// <returns>The caller.</returns>
    public static PluginCaller Holding(string tenant, string subject, params PluginPermission[] permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        return new PluginCaller(tenant, subject) { Permissions = permissions };
    }

    /// <summary>Builds an unauthenticated caller, which every guarded transition refuses.</summary>
    /// <param name="tenant">The tenant the request arrived for.</param>
    /// <returns>The caller.</returns>
    public static PluginCaller Anonymous(string tenant) =>
        new(tenant, "anonymous") { IsAuthenticated = false };

    /// <summary>Determines whether the caller holds a permission.</summary>
    /// <param name="permission">The permission being asked for.</param>
    /// <returns><see langword="true"/> when the caller holds it.</returns>
    public bool Holds(PluginPermission permission) =>
        Permissions.Any(held => held.Grants(permission));

    /// <inheritdoc />
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Tenant}|{Subject}");
}
