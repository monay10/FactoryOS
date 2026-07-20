using FactoryOS.Persistence.Configuration;
using FactoryOS.Persistence.Migrations;

namespace FactoryOS.Tests.Persistence;

public sealed class PersistenceOptionsTests
{
    [Fact]
    public void Defaults_target_the_sqlite_development_provider()
    {
        var options = new PersistenceOptions();

        Assert.Equal(DatabaseProvider.Sqlite, options.Provider);
        Assert.Equal(PersistenceConstants.DefaultCommandTimeoutSeconds, options.CommandTimeoutSeconds);
        Assert.Equal(PersistenceConstants.DefaultMaxRetryCount, options.MaxRetryCount);
        Assert.False(options.ApplyMigrationsOnStartup);
    }

    [Fact]
    public void The_migration_assembly_resolves_to_the_context_assembly_by_default()
    {
        var resolved = MigrationAssemblyResolver.Resolve(typeof(PersistenceOptionsTests), new PersistenceOptions());

        Assert.Equal(typeof(PersistenceOptionsTests).Assembly.GetName().Name, resolved);
    }

    [Fact]
    public void An_explicit_migration_assembly_overrides_the_default()
    {
        var options = new PersistenceOptions { MigrationsAssembly = "FactoryOS.Migrations" };

        var resolved = MigrationAssemblyResolver.Resolve(typeof(PersistenceOptionsTests), options);

        Assert.Equal("FactoryOS.Migrations", resolved);
    }
}
