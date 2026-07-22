using FactoryOS.Contracts.Plugins;

namespace FactoryOS.Plugins.Runtime.Configuration;

/// <summary>
/// Stable constants for the plugin runtime: the configuration section it binds from, the conventional
/// file names it looks for on disk, and the label names its measurements carry.
/// </summary>
public static class PluginRuntimeConstants
{
    /// <summary>The configuration section the <see cref="PluginRuntimeOptions"/> bind from.</summary>
    public const string ConfigurationSection = "Plugins:Runtime";

    /// <summary>The conventional manifest file name in every plugin package folder.</summary>
    public const string ManifestFileName = "module.json";

    /// <summary>The conventional detached-signature file name beside a package manifest.</summary>
    public const string SignatureFileName = "module.sig";

    /// <summary>The measurement label carrying the plugin key.</summary>
    public const string PluginLabel = "plugin";

    /// <summary>The measurement label carrying the tenant.</summary>
    public const string TenantLabel = "tenant";

    /// <summary>The measurement label carrying the lifecycle phase.</summary>
    public const string PhaseLabel = "phase";

    /// <summary>The measurement label carrying whether the transition succeeded.</summary>
    public const string OutcomeLabel = "outcome";
}

/// <summary>
/// The quota one plugin instance may consume. A quota is per <b>instance</b>, not per plugin: one factory
/// running a heavy report must not be able to spend another factory's budget on the same plugin.
/// </summary>
public sealed record PluginResourceQuota
{
    /// <summary>Gets the quota that imposes no limit at all.</summary>
    public static PluginResourceQuota Unlimited { get; } = new()
    {
        MaxConcurrentOperations = 0,
        MaxMemoryBytes = 0,
        MaxStorageBytes = 0,
    };

    /// <summary>Gets the number of operations the instance may run at once; <c>0</c> means unlimited.</summary>
    public int MaxConcurrentOperations { get; init; }

    /// <summary>Gets the memory the instance may hold, in bytes; <c>0</c> means unlimited.</summary>
    public long MaxMemoryBytes { get; init; }

    /// <summary>Gets the storage the instance may occupy, in bytes; <c>0</c> means unlimited.</summary>
    public long MaxStorageBytes { get; init; }

    /// <summary>Determines whether a limit is set for a resource.</summary>
    /// <param name="limit">The configured limit.</param>
    /// <returns><see langword="true"/> when the limit is enforced.</returns>
    public static bool Enforces(long limit) => limit > 0;
}

/// <summary>
/// The plugin runtime options, bound from <see cref="PluginRuntimeConstants.ConfigurationSection"/>.
/// <para>
/// These are <b>platform</b> settings, never per-customer ones: which folder packages are read from, whether
/// a signature is mandatory, how many superseded versions are retained so a rollback has somewhere to go.
/// Which plugins a factory runs is a tenant's own configuration, not a setting here.
/// </para>
/// </summary>
public sealed class PluginRuntimeOptions
{
    /// <summary>
    /// Gets or sets the platform version packages declare their compatibility against.
    /// <para>
    /// It is configuration rather than a compiled constant so a host can pin what it advertises: an operator
    /// upgrading the platform can hold the advertised version back for one release while third-party
    /// packages catch up, instead of every one of them failing compatibility on the same morning.
    /// </para>
    /// </summary>
    public string PlatformVersion { get; set; } = "1.0.0";

    /// <summary>Gets or sets the directory whose immediate subfolders each hold one plugin package.</summary>
    public string PackageRoot { get; set; } = "plugins";

    /// <summary>Gets or sets the directory under which each instance is given its own isolated storage.</summary>
    public string StorageRoot { get; set; } = "data/plugins";

    /// <summary>
    /// Gets or sets a value indicating whether a package must carry a valid signature to be installed.
    /// <para>
    /// An <b>invalid</b> signature is always fatal. This switch governs only the <b>absent</b> one: first-party
    /// packages built into the monolith ship unsigned, while a Store package must never be trusted unsigned.
    /// </para>
    /// </summary>
    public bool RequireSignature { get; set; }

    /// <summary>Gets or sets how many superseded package versions are retained so a rollback has a target.</summary>
    public int RetainedVersions { get; set; } = 1;

    /// <summary>Gets or sets the grace period after a start during which health is not yet judged.</summary>
    public TimeSpan HealthGracePeriod { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets how long the runtime waits for a plugin to honour a stop before it is faulted.</summary>
    public TimeSpan StopTimeout { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>Gets or sets the interval at which a scheduled health probe becomes due.</summary>
    public TimeSpan HealthProbeInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Gets or sets a value indicating whether lifecycle transitions are published as runtime events.</summary>
    public bool PublishLifecycleEvents { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether successful lifecycle transitions are audited too.</summary>
    public bool AuditLifecycle { get; set; } = true;

    /// <summary>Gets or sets the default quota applied to an instance that does not narrow it further.</summary>
    public PluginResourceQuota DefaultQuota { get; set; } = PluginResourceQuota.Unlimited;

    /// <summary>Reads <see cref="PluginVersion"/> the host advertises as the platform version.</summary>
    /// <returns>The parsed platform version.</returns>
    /// <exception cref="FormatException">Thrown when the configured value is not a valid version.</exception>
    public PluginVersion Platform() => PluginVersion.Parse(PlatformVersion);
}
