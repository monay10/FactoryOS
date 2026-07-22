using System.Collections.Concurrent;
using System.Globalization;
using FactoryOS.Plugin.Isolation;
using FactoryOS.Plugins.Runtime.Configuration;
using FactoryOS.Plugins.Runtime.Domain;
using Microsoft.Extensions.Options;

namespace FactoryOS.Plugins.Runtime.Isolation;

/// <summary>
/// Everything that separates one tenant's plugin from every other: where its assemblies load, where its
/// configuration comes from, where its files live, what it may do and how much it may use.
/// </summary>
/// <param name="Tenant">The tenant the scope belongs to.</param>
/// <param name="PluginKey">The plugin.</param>
/// <param name="Version">The version installed.</param>
/// <param name="LoadContextName">The name of the assembly load context the plugin's own dependencies use.</param>
/// <param name="ConfigurationSection">The configuration section the plugin's settings are read from.</param>
/// <param name="StoragePath">The directory that belongs to this instance alone.</param>
/// <param name="Permissions">What the plugin may actually do here.</param>
/// <param name="Quota">How much it may consume.</param>
public sealed record PluginIsolationScope(
    string Tenant,
    string PluginKey,
    Contracts.Plugins.PluginVersion Version,
    string LoadContextName,
    string ConfigurationSection,
    string StoragePath,
    IReadOnlyList<PluginPermission> Permissions,
    PluginResourceQuota Quota)
{
    /// <inheritdoc />
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Tenant}|{PluginKey}@{Version}");
}

/// <summary>
/// Derives each instance's isolation scope and owns the assembly load contexts.
/// <para>
/// The framework can <b>load</b> a plugin: <see cref="FactoryOS.Plugin.Loading.ModuleLoader"/> creates a
/// collectible <see cref="PluginLoadContext"/>, loads the entry assembly and lets the context go. What it
/// cannot do is <b>unload</b> one, because nothing holds a reference to ask. This manager keeps that
/// reference, per <c>tenant|plugin</c>, which is what makes update, rollback and remove possible without
/// restarting the host.
/// </para>
/// <para>
/// The honest limit: requesting an unload begins a collection that completes only once nothing references
/// anything the context loaded. The runtime therefore reports that the release was <i>requested</i>, and
/// never claims the assembly is gone.
/// </para>
/// </summary>
public sealed class PluginIsolationManager
{
    private readonly ConcurrentDictionary<string, PluginLoadContext> _contexts =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly PluginRuntimeOptions _options;

    /// <summary>Initializes a new instance of the <see cref="PluginIsolationManager"/> class.</summary>
    /// <param name="options">The runtime options.</param>
    public PluginIsolationManager(IOptions<PluginRuntimeOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    /// <summary>Gets the number of load contexts currently held.</summary>
    public int LoadedContexts => _contexts.Count;

    /// <summary>Derives the isolation scope of one instance.</summary>
    /// <param name="instance">The tenant's installation.</param>
    /// <param name="definition">The definition it installs.</param>
    /// <returns>The scope.</returns>
    public PluginIsolationScope Scope(PluginInstance instance, PluginDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(definition);

        return new PluginIsolationScope(
            instance.Tenant,
            instance.PluginKey,
            instance.Version,
            LoadContextNameFor(instance),
            instance.Settings.Section,
            StoragePathFor(instance),
            instance.EffectivePermissions(definition),
            instance.Quota);
    }

    /// <summary>
    /// Builds the directory an instance owns.
    /// <para>
    /// The path deliberately carries the tenant and the plugin but <b>not the version</b>. A plugin's data
    /// has to survive the update that changes its code, and a rollback has to find the data the newer version
    /// left behind — versioned storage would silently give an updated plugin an empty disk.
    /// </para>
    /// </summary>
    /// <param name="instance">The tenant's installation.</param>
    /// <returns>The directory path.</returns>
    public string StoragePathFor(PluginInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return Path.Combine(_options.StorageRoot, instance.Tenant, instance.PluginKey);
    }

    /// <summary>Builds the load-context name an instance's private dependencies load under.</summary>
    /// <param name="instance">The tenant's installation.</param>
    /// <returns>The name.</returns>
    public static string LoadContextNameFor(PluginInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"PluginRuntime:{instance.Tenant}|{instance.PluginKey}@{instance.Version}");
    }

    /// <summary>Keeps the load context an instance's assemblies were loaded into.</summary>
    /// <param name="instance">The tenant's installation.</param>
    /// <param name="context">The load context.</param>
    public void Attach(PluginInstance instance, PluginLoadContext context)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(context);
        _contexts[instance.Identity] = context;
    }

    /// <summary>Determines whether an instance's assemblies are held in their own context.</summary>
    /// <param name="instance">The tenant's installation.</param>
    /// <returns><see langword="true"/> when a context is held for it.</returns>
    public bool IsIsolated(PluginInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return _contexts.ContainsKey(instance.Identity);
    }

    /// <summary>
    /// Requests the release of an instance's load context. The request is what the runtime can guarantee;
    /// the collection completes when nothing references what the context loaded.
    /// </summary>
    /// <param name="instance">The tenant's installation.</param>
    /// <returns><see langword="true"/> when a context was held and its release was requested.</returns>
    public bool Release(PluginInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        if (!_contexts.TryRemove(instance.Identity, out var context))
        {
            return false;
        }

        context.Unload();
        return true;
    }

    /// <summary>Requests the release of every context one tenant holds.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>How many contexts were released.</returns>
    public int ReleaseTenant(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);

        var prefix = tenant + "|";
        var released = 0;

        foreach (var identity in _contexts.Keys
            .Where(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToArray())
        {
            if (_contexts.TryRemove(identity, out var context))
            {
                context.Unload();
                released++;
            }
        }

        return released;
    }
}
