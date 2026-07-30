using FactoryOS.Api.Persistence.Audit;
using FactoryOS.Persistence.Multitenancy;
using FactoryOS.Plugins.Workflow.Audit.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactoryOS.IntegrationTests.Persistence;

/// <summary>
/// Exercises <see cref="EfAuditStore"/> against a real EF Core pipeline (SQLite in-memory), proving a tenant's
/// audit records survive being appended and read back through a fresh store — the test-level equivalent of
/// surviving a restart — that the store never reads across tenants, and, above all, that the tamper-evident hash
/// chain reloads intact: every reloaded record's recomputed hash still matches the hash it was sealed with.
/// </summary>
public sealed class EfAuditStoreTests : IDisposable
{
    private static readonly DateTimeOffset Occurred =
        new DateTimeOffset(2026, 7, 30, 8, 0, 0, 123, TimeSpan.Zero).AddTicks(4567);

    private readonly SqliteContextFactory _factory = new();

    private static AuditRecord SealRich(string tenant, long sequence, string previousHash)
    {
        var entry = new AuditEntry
        {
            Category = AuditCategory.Configuration,
            Action = AuditAction.Changed,
            Target = new AuditTarget(AuditTargetType.Configuration, "energy.thresholds", "row-7"),
            Scope = new AuditScope(tenant, "istanbul-plant", "energy"),
            Actor = AuditActor.User("u-42", "Ada Lovelace"),
            Severity = AuditSeverity.Notice,
            Result = AuditResult.Success,
            Correlation = new AuditCorrelation("corr-1", "trace-9", "sess-3", "req-8", "cause-2"),
            EventType = "ConfigurationChanged",
            Message = "Energy thresholds changed.",
            Snapshot = new AuditSnapshot(
                new Dictionary<string, string?> { ["max"] = "80", ["min"] = null },
                new Dictionary<string, string?> { ["max"] = "90", ["min"] = "10" }),
            Metadata = AuditMetadata.Empty.With("source", "console").With("reviewed", "true"),
            Tags = [AuditTag.Of("Energy"), AuditTag.Of("Threshold")],
            OccurredOnUtc = Occurred,
        };

        return AuditRecord.Seal(entry, sequence, previousHash, Occurred.AddMinutes(1));
    }

    [Fact]
    public void Append_then_Get_round_trips_a_full_record_with_its_chain_intact()
    {
        var store = new EfAuditStore(_factory);
        var sealed_ = SealRich("acme", 1, AuditRecord.GenesisHash);
        store.Append(sealed_);

        var found = store.Get(sealed_.Id);

        Assert.NotNull(found);
        Assert.Equal(sealed_.Sequence, found!.Sequence);
        Assert.Equal("acme", found.Tenant);
        Assert.Equal("istanbul-plant", found.Scope.Organization);
        Assert.Equal(AuditAction.Changed, found.Action);
        Assert.Equal("Ada Lovelace", found.Actor.DisplayName);
        Assert.Equal("row-7", found.Target.Id);
        Assert.Equal("trace-9", found.Correlation.TraceId);
        Assert.Equal("console", found.Metadata["source"]);
        Assert.Equal(2, found.Tags.Count);
        Assert.Equal(Occurred, found.OccurredOnUtc);
        Assert.NotNull(found.Snapshot);
        Assert.Equal("90", found.Snapshot!.After["max"]);

        // The load-bearing proof: the record reloaded byte-exact, so its recomputed hash still matches the sealed
        // hash. If persistence had truncated a timestamp or dropped a field, this would break.
        Assert.Equal(sealed_.Hash, found.Hash);
        Assert.Equal(found.Hash, found.RecomputeHash());
    }

    [Fact]
    public void A_fresh_store_over_the_same_database_reads_what_a_prior_store_appended()
    {
        var first = new EfAuditStore(_factory);
        var record = SealRich("acme", 1, AuditRecord.GenesisHash);
        first.Append(record);

        // A brand-new store instance over the same database — the closest in-memory analog of a process restart.
        var reopened = new EfAuditStore(_factory);
        var found = reopened.Get(record.Id);

        Assert.NotNull(found);
        Assert.Equal(found!.Hash, found.RecomputeHash());
    }

    [Fact]
    public void The_hash_chain_verifies_after_a_reload()
    {
        var store = new EfAuditStore(_factory);
        var first = SealRich("acme", 1, AuditRecord.GenesisHash);
        var second = SealRich("acme", 2, first.Hash);
        var third = SealRich("acme", 3, second.Hash);
        store.Append(first);
        store.Append(second);
        store.Append(third);

        var reloaded = new EfAuditStore(_factory).ListByTenant("acme");

        Assert.Equal(3, reloaded.Count);
        var previousHash = AuditRecord.GenesisHash;
        foreach (var record in reloaded)
        {
            Assert.Equal(previousHash, record.PreviousHash);
            Assert.Equal(record.Hash, record.RecomputeHash());
            previousHash = record.Hash;
        }
    }

    [Fact]
    public void Head_returns_the_latest_record_and_the_store_never_reads_across_tenants()
    {
        var store = new EfAuditStore(_factory);
        store.Append(SealRich("acme", 1, AuditRecord.GenesisHash));
        var acmeHead = SealRich("acme", 2, "prev");
        store.Append(acmeHead);
        store.Append(SealRich("globex", 1, AuditRecord.GenesisHash));

        Assert.Equal(acmeHead.Id, store.Head("acme")!.Id);
        Assert.Equal(2, store.ListByTenant("acme").Count);
        Assert.Single(store.ListByTenant("globex"));
        Assert.Equal(3, store.All().Count);
    }

    [Fact]
    public void Remove_deletes_the_named_records_and_counts_them()
    {
        var store = new EfAuditStore(_factory);
        var first = SealRich("acme", 1, AuditRecord.GenesisHash);
        var second = SealRich("acme", 2, first.Hash);
        store.Append(first);
        store.Append(second);

        Assert.Equal(1, store.Remove([first.Id]));
        Assert.Null(store.Get(first.Id));
        Assert.NotNull(store.Get(second.Id));
        Assert.Equal(0, store.Remove([first.Id]));
    }

    public void Dispose() => _factory.Dispose();

    /// <summary>A context factory over one kept-open SQLite in-memory connection, shared by every context.</summary>
    private sealed class SqliteContextFactory : IDbContextFactory<AuditDbContext>, IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ITenantSchemaProvider _schema = new FixedTenantSchemaProvider("public");

        public SqliteContextFactory()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
        }

        public AuditDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<AuditDbContext>()
                .UseSqlite(_connection)
                .Options;
            return new AuditDbContext(options, _schema);
        }

        public void Dispose() => _connection.Dispose();
    }
}
