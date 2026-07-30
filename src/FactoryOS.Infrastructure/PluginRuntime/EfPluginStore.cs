using System.Linq;
using System.Text.Json;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Plugins.Runtime.Configuration;
using FactoryOS.Plugins.Runtime.Domain;
using FactoryOS.Plugins.Runtime.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactoryOS.Infrastructure.PluginRuntime;

/// <summary>
/// An EF Core <see cref="IPluginStore"/> — the tenant installations that must survive a restart, persisted to the
/// configured relational database. It is the platform's first domain persistence and the adapter a host swaps in
/// for the runtime's in-memory default when a database is configured.
/// <para>
/// The store is a singleton (the runtime resolves it as one), so it holds a <see cref="IDbContextFactory{TContext}"/>
/// and opens a short-lived context per operation rather than capturing a scoped one. Mapping to and from the domain
/// is explicit: the rich <see cref="PluginInstance"/> is flattened to a <see cref="PluginInstallationRecord"/> and
/// rehydrated through <see cref="PluginInstance.Rehydrate"/>, so a reloaded installation is indistinguishable from a
/// live one.
/// </para>
/// </summary>
public sealed class EfPluginStore : IPluginStore
{
    private readonly IDbContextFactory<PluginRuntimeDbContext> _factory;

    /// <summary>Initializes the store and ensures its schema exists.</summary>
    /// <param name="factory">The context factory the store opens a context from per operation.</param>
    public EfPluginStore(IDbContextFactory<PluginRuntimeDbContext> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;

        // Create the installations table on first construction. Idempotent, so a second store over the same
        // database is a no-op. Real Postgres migrations are a later concern; this makes the store self-sufficient.
        using var context = _factory.CreateDbContext();
        context.Database.EnsureCreated();
    }

    /// <inheritdoc />
    public void Save(PluginInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        using var context = _factory.CreateDbContext();
        var record = ToRecord(instance);
        var existing = context.Installations.Find(instance.Tenant, instance.PluginKey);
        if (existing is null)
        {
            context.Installations.Add(record);
        }
        else
        {
            context.Entry(existing).CurrentValues.SetValues(record);
        }

        context.SaveChanges();
    }

    /// <inheritdoc />
    public PluginInstance? Find(string tenant, string pluginKey)
    {
        using var context = _factory.CreateDbContext();
        var record = context.Installations.AsNoTracking()
            .FirstOrDefault(row => row.Tenant == tenant && row.PluginKey == pluginKey);
        return record is null ? null : ToDomain(record);
    }

    /// <inheritdoc />
    public IReadOnlyList<PluginInstance> ForTenant(string tenant)
    {
        using var context = _factory.CreateDbContext();
        return [.. context.Installations.AsNoTracking()
            .Where(row => row.Tenant == tenant)
            .ToList()
            .Select(ToDomain)];
    }

    /// <inheritdoc />
    public IReadOnlyList<PluginInstance> All()
    {
        using var context = _factory.CreateDbContext();
        return [.. context.Installations.AsNoTracking().ToList().Select(ToDomain)];
    }

    /// <inheritdoc />
    public bool Remove(string tenant, string pluginKey)
    {
        using var context = _factory.CreateDbContext();
        var record = context.Installations.Find(tenant, pluginKey);
        if (record is null)
        {
            return false;
        }

        context.Installations.Remove(record);
        context.SaveChanges();
        return true;
    }

    private static PluginInstallationRecord ToRecord(PluginInstance instance) => new()
    {
        Tenant = instance.Tenant,
        PluginKey = instance.PluginKey,
        Version = instance.Version.ToString(),
        PreviousVersion = instance.PreviousVersion?.ToString(),
        Grants = string.Join(';', instance.Granted.Select(permission => permission.ToString())),
        SettingsJson = JsonSerializer.Serialize(instance.Settings.Values),
        MaxConcurrentOperations = instance.Quota.MaxConcurrentOperations,
        MaxMemoryBytes = instance.Quota.MaxMemoryBytes,
        MaxStorageBytes = instance.Quota.MaxStorageBytes,
        Status = instance.Status.ToString(),
        Enabled = instance.Enabled,
        FailureReason = instance.FailureReason,
        FailureKind = instance.FailureKind.ToString(),
        StartedUtc = instance.StartedUtc,
    };

    private static PluginInstance ToDomain(PluginInstallationRecord record)
    {
        IEnumerable<PluginPermission> grants = string.IsNullOrEmpty(record.Grants)
            ? Array.Empty<PluginPermission>()
            : record.Grants.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(PluginPermission.Parse);

        var values = JsonSerializer.Deserialize<Dictionary<string, string?>>(record.SettingsJson)
            ?? new Dictionary<string, string?>();

        var settings = new PluginSettings(record.Tenant, record.PluginKey, values);

        var quota = new PluginResourceQuota
        {
            MaxConcurrentOperations = record.MaxConcurrentOperations,
            MaxMemoryBytes = record.MaxMemoryBytes,
            MaxStorageBytes = record.MaxStorageBytes,
        };

        return PluginInstance.Rehydrate(
            record.Tenant,
            record.PluginKey,
            PluginVersion.Parse(record.Version),
            string.IsNullOrEmpty(record.PreviousVersion) ? null : PluginVersion.Parse(record.PreviousVersion),
            grants,
            settings,
            quota,
            Enum.Parse<PluginRuntimeStatus>(record.Status),
            record.Enabled,
            record.FailureReason,
            Enum.Parse<PluginFailureKind>(record.FailureKind),
            record.StartedUtc);
    }
}
