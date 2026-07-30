using FactoryOS.Contracts.Plugins;
using FactoryOS.Infrastructure.PluginRuntime;
using FactoryOS.Persistence.Multitenancy;
using FactoryOS.Plugins.Runtime.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactoryOS.IntegrationTests.Persistence;

/// <summary>
/// Exercises <see cref="EfPluginStore"/> against a real EF Core pipeline (SQLite in-memory), proving a tenant's
/// installation survives being written and read back through a fresh store — the test-level equivalent of
/// surviving a restart — and that the store never reads across tenants.
/// </summary>
public sealed class EfPluginStoreTests : IDisposable
{
    private static readonly DateTimeOffset Started = new(2026, 7, 30, 8, 0, 0, TimeSpan.Zero);
    private readonly SqliteContextFactory _factory = new();

    private static PluginInstance RunningInstall(string tenant, string key, string version)
    {
        var instance = new PluginInstance(
            tenant, key, PluginVersion.Parse(version),
            new[] { PluginPermission.Parse("uimetadata.extend") },
            new PluginSettings(tenant, key, new Dictionary<string, string?> { ["region"] = "eu" }));
        instance.MarkInstalled();
        instance.MarkLoaded();
        instance.MarkRunning(Started);
        return instance;
    }

    [Fact]
    public void Save_then_Find_round_trips_the_full_installation()
    {
        var store = new EfPluginStore(_factory);
        store.Save(RunningInstall("acme", "demo", "1.2.0"));

        var found = store.Find("acme", "demo");

        Assert.NotNull(found);
        Assert.Equal(PluginVersion.Parse("1.2.0"), found!.Version);
        Assert.Equal(PluginRuntimeStatus.Running, found.Status);
        Assert.Equal(Started, found.StartedUtc);
        Assert.Single(found.Granted);
        Assert.Equal("uimetadata.extend", found.Granted[0].ToString());
        Assert.Equal("eu", found.Settings.Get("region"));
    }

    [Fact]
    public void A_fresh_store_over_the_same_database_reads_what_a_prior_store_saved()
    {
        new EfPluginStore(_factory).Save(RunningInstall("acme", "demo", "1.0.0"));

        // A brand-new store instance over the same database — the closest in-memory analog of a process restart.
        var reopened = new EfPluginStore(_factory);

        Assert.NotNull(reopened.Find("acme", "demo"));
    }

    [Fact]
    public void The_store_lists_a_tenant_s_installs_and_never_reads_across_tenants()
    {
        var store = new EfPluginStore(_factory);
        store.Save(RunningInstall("acme", "demo", "1.0.0"));
        store.Save(RunningInstall("acme", "energy", "1.0.0"));
        store.Save(RunningInstall("globex", "demo", "1.0.0"));

        Assert.Equal(2, store.ForTenant("acme").Count);
        Assert.Equal(3, store.All().Count);
        Assert.Null(store.Find("globex", "energy"));
        Assert.NotNull(store.Find("globex", "demo"));
    }

    [Fact]
    public void Remove_deletes_the_installation_and_reports_whether_there_was_one()
    {
        var store = new EfPluginStore(_factory);
        store.Save(RunningInstall("acme", "demo", "1.0.0"));

        Assert.True(store.Remove("acme", "demo"));
        Assert.Null(store.Find("acme", "demo"));
        Assert.False(store.Remove("acme", "demo"));
    }

    public void Dispose() => _factory.Dispose();

    /// <summary>A context factory over one kept-open SQLite in-memory connection, shared by every context.</summary>
    private sealed class SqliteContextFactory : IDbContextFactory<PluginRuntimeDbContext>, IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ITenantSchemaProvider _schema = new FixedTenantSchemaProvider("public");

        public SqliteContextFactory()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
        }

        public PluginRuntimeDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<PluginRuntimeDbContext>()
                .UseSqlite(_connection)
                .Options;
            return new PluginRuntimeDbContext(options, _schema);
        }

        public void Dispose() => _connection.Dispose();
    }
}
