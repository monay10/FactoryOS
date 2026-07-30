using FactoryOS.Persistence.Context;
using FactoryOS.Persistence.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace FactoryOS.Infrastructure.PluginRuntime;

/// <summary>
/// The persistable state of one tenant's plugin installation. It is deliberately a flat record separate from the
/// rich <c>PluginInstance</c> domain object: value objects are flattened to columns (version and grants to
/// strings, settings to JSON, the quota to three numbers) so the store maps explicitly rather than mapping the
/// aggregate's private state. The primary key is the same identity the domain files an instance under —
/// <c>(Tenant, PluginKey)</c> — so a tenant can never reach another's row.
/// </summary>
public sealed class PluginInstallationRecord
{
    /// <summary>The tenant that owns the installation.</summary>
    public string Tenant { get; set; } = string.Empty;

    /// <summary>The plugin key.</summary>
    public string PluginKey { get; set; } = string.Empty;

    /// <summary>The installed version, as text.</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>The version an update replaced, as text; null when none.</summary>
    public string? PreviousVersion { get; set; }

    /// <summary>The granted permissions, as a <c>;</c>-joined list of <c>resource.action</c> strings.</summary>
    public string Grants { get; set; } = string.Empty;

    /// <summary>The tenant's settings for the plugin, as a JSON object.</summary>
    public string SettingsJson { get; set; } = "{}";

    /// <summary>The quota's maximum concurrent operations (0 = unlimited).</summary>
    public int MaxConcurrentOperations { get; set; }

    /// <summary>The quota's maximum memory in bytes (0 = unlimited).</summary>
    public long MaxMemoryBytes { get; set; }

    /// <summary>The quota's maximum storage in bytes (0 = unlimited).</summary>
    public long MaxStorageBytes { get; set; }

    /// <summary>The lifecycle status, as its enum name.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Whether the plugin is switched on for the tenant.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Why the installation failed, when it has.</summary>
    public string? FailureReason { get; set; }

    /// <summary>How the installation failed, as its enum name.</summary>
    public string FailureKind { get; set; } = string.Empty;

    /// <summary>When the installation last started running, when it has.</summary>
    public DateTimeOffset? StartedUtc { get; set; }
}

/// <summary>
/// The plugin runtime's persistence context — the first domain persistence in the platform. It inherits the
/// FactoryOS schema-per-tenant and convention pipeline from <see cref="FactoryOsDbContext"/> and maps a single
/// table, the tenant plugin installations.
/// </summary>
public sealed class PluginRuntimeDbContext : FactoryOsDbContext
{
    /// <summary>Initializes a new instance of the <see cref="PluginRuntimeDbContext"/> class.</summary>
    /// <param name="options">The context options.</param>
    /// <param name="schemaProvider">The tenant schema provider.</param>
    public PluginRuntimeDbContext(
        DbContextOptions<PluginRuntimeDbContext> options, ITenantSchemaProvider schemaProvider)
        : base(options, schemaProvider)
    {
    }

    /// <summary>The tenant plugin installations.</summary>
    public DbSet<PluginInstallationRecord> Installations => Set<PluginInstallationRecord>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<PluginInstallationRecord>(entity =>
        {
            entity.ToTable("plugin_installations");
            entity.HasKey(record => new { record.Tenant, record.PluginKey });
            entity.Property(record => record.Tenant).IsRequired();
            entity.Property(record => record.PluginKey).IsRequired();
            entity.Property(record => record.Version).IsRequired();
            entity.Property(record => record.Grants).IsRequired();
            entity.Property(record => record.SettingsJson).IsRequired();
            entity.Property(record => record.Status).IsRequired();
            entity.Property(record => record.FailureKind).IsRequired();
        });

        // Base conventions (schema, soft-delete filter, concurrency token) applied last.
        base.OnModelCreating(modelBuilder);
    }
}
