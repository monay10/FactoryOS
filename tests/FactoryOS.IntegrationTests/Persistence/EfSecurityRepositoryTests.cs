using System.Linq;
using FactoryOS.Api.Persistence.Security;
using FactoryOS.Persistence.Multitenancy;
using FactoryOS.Plugins.Workflow.Security.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactoryOS.IntegrationTests.Persistence;

/// <summary>
/// Exercises <see cref="EfSecurityRepository"/> against a real EF Core pipeline (SQLite in-memory), proving a
/// tenant's direct grants survive being written and read back through a fresh repository — the test-level
/// equivalent of surviving a restart — that a grant made in one tenant is unreachable from another, and that the
/// deliberately non-persisted policies and roles do <b>not</b> survive a fresh repository.
/// </summary>
public sealed class EfSecurityRepositoryTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    [Fact]
    public void Grant_then_GrantsFor_round_trips_and_is_idempotent()
    {
        var repository = new EfSecurityRepository(_factory);
        repository.Grant("acme", "u-1", "energy.read");
        repository.Grant("acme", "u-1", "energy.write");
        repository.Grant("acme", "u-1", "energy.read"); // repeated — must not duplicate

        var grants = repository.GrantsFor("acme", "u-1");

        Assert.Equal(["energy.read", "energy.write"], grants);
    }

    [Fact]
    public void A_fresh_repository_over_the_same_database_reads_what_a_prior_one_granted()
    {
        new EfSecurityRepository(_factory).Grant("acme", "u-1", "energy.read");

        // A brand-new repository over the same database — the closest in-memory analog of a process restart.
        var reopened = new EfSecurityRepository(_factory);

        Assert.Equal(["energy.read"], reopened.GrantsFor("acme", "u-1"));
    }

    [Fact]
    public void Revoke_removes_the_grant_and_reports_whether_there_was_one()
    {
        var repository = new EfSecurityRepository(_factory);
        repository.Grant("acme", "u-1", "energy.read");

        Assert.True(repository.Revoke("acme", "u-1", "energy.read"));
        Assert.Empty(repository.GrantsFor("acme", "u-1"));
        Assert.False(repository.Revoke("acme", "u-1", "energy.read"));
    }

    [Fact]
    public void A_grant_in_one_tenant_is_unreachable_from_another()
    {
        var repository = new EfSecurityRepository(_factory);
        repository.Grant("acme", "u-1", "energy.read");

        Assert.Empty(repository.GrantsFor("globex", "u-1"));
        Assert.False(repository.Revoke("globex", "u-1", "energy.read"));
        Assert.Equal(["energy.read"], repository.GrantsFor("acme", "u-1"));
    }

    [Fact]
    public void A_fresh_repository_reads_back_a_tenant_s_whole_grants_roster_grouped_and_ordered()
    {
        var writer = new EfSecurityRepository(_factory);
        writer.Grant("acme", "u-2", "quality.read");
        writer.Grant("acme", "u-1", "energy.write");
        writer.Grant("acme", "u-1", "energy.read");
        writer.Grant("globex", "u-9", "safety.read"); // another tenant — must not appear in acme's roster

        // A brand-new repository over the same database — the restart analog.
        var entries = new EfSecurityRepository(_factory).GrantsIn("acme");

        var flat = entries.Select(entry => $"{entry.Subject}/{entry.Permission}").ToArray();
        Assert.Equal(["u-1/energy.read", "u-1/energy.write", "u-2/quality.read"], flat);
    }

    [Fact]
    public void Policies_and_roles_are_not_persisted_across_a_fresh_repository()
    {
        var repository = new EfSecurityRepository(_factory);
        repository.RegisterRole(SecurityRole.Of("operator", "Operator", "energy.read"));
        Assert.NotNull(repository.FindRole("operator"));

        // Policies and roles are config-rebuilt at start-up, not persisted — a fresh repository has none.
        Assert.Null(new EfSecurityRepository(_factory).FindRole("operator"));
    }

    public void Dispose() => _factory.Dispose();

    /// <summary>A context factory over one kept-open SQLite in-memory connection, shared by every context.</summary>
    private sealed class SqliteContextFactory : IDbContextFactory<SecurityDbContext>, IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ITenantSchemaProvider _schema = new FixedTenantSchemaProvider("public");

        public SqliteContextFactory()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
        }

        public SecurityDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<SecurityDbContext>()
                .UseSqlite(_connection)
                .Options;
            return new SecurityDbContext(options, _schema);
        }

        public void Dispose() => _connection.Dispose();
    }
}
