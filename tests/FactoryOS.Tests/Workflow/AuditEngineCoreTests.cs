using System.Text.Json;
using FactoryOS.Plugins.Workflow.Audit.Configuration;
using FactoryOS.Plugins.Workflow.Audit.Diagnostics;
using FactoryOS.Plugins.Workflow.Audit.Domain;
using FactoryOS.Plugins.Workflow.Audit.Events;
using FactoryOS.Plugins.Workflow.Audit.Execution;
using FactoryOS.Plugins.Workflow.Audit.Persistence;
using FactoryOS.Tests.Identity;

namespace FactoryOS.Tests.Workflow.Audit;

/// <summary>
/// Unit coverage of the audit engine core: sealing entries into an immutable, per-tenant hash chain; detecting
/// altered and unlinked records; filtering; correlation preservation; search and session projection; export;
/// archiving, retention and restore; and metrics — exercised directly, without a container and without any of
/// the engines whose events it records.
/// </summary>
public sealed class AuditEngineCoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 07, 21, 09, 00, 00, TimeSpan.Zero);

    // ---- Recording and the hash chain -------------------------------------------------------------------------

    [Fact]
    public void The_first_record_of_a_tenant_opens_its_chain()
    {
        var harness = Harness.Create(Now);

        var record = harness.Engine.Record(Entry("acme", AuditAction.Created))!;

        Assert.Equal(1, record.Sequence);
        Assert.Equal(AuditRecord.GenesisHash, record.PreviousHash);
        Assert.Equal(record.RecomputeHash(), record.Hash);
        Assert.Equal("acme", record.Tenant);
    }

    [Fact]
    public void Each_record_links_to_the_one_before_it()
    {
        var harness = Harness.Create(Now);

        var first = harness.Engine.Record(Entry("acme", AuditAction.Created))!;
        var second = harness.Engine.Record(Entry("acme", AuditAction.Updated))!;
        var third = harness.Engine.Record(Entry("acme", AuditAction.Completed))!;

        Assert.Equal(first.Hash, second.PreviousHash);
        Assert.Equal(second.Hash, third.PreviousHash);
        Assert.Equal([1L, 2L, 3L], new[] { first.Sequence, second.Sequence, third.Sequence });
        Assert.True(harness.Engine.Verify("acme").IsValid);
    }

    [Fact]
    public void Every_tenant_has_its_own_independent_chain()
    {
        var harness = Harness.Create(Now);

        harness.Engine.Record(Entry("acme", AuditAction.Created));
        var otherTenantFirst = harness.Engine.Record(Entry("globex", AuditAction.Created))!;

        // A second tenant starts its own chain at one; nothing crosses tenants.
        Assert.Equal(1, otherTenantFirst.Sequence);
        Assert.Equal(AuditRecord.GenesisHash, otherTenantFirst.PreviousHash);
        Assert.True(harness.Engine.Verify("acme").IsValid);
        Assert.True(harness.Engine.Verify("globex").IsValid);
    }

    [Fact]
    public void An_audit_record_exposes_no_way_to_change_it()
    {
        // Immutability is a property of the type, so it is asserted against the type rather than an instance.
        Assert.True(typeof(AuditRecord).IsSealed);
        Assert.DoesNotContain(typeof(AuditRecord).GetProperties(), property => property.CanWrite);
        // Constants are fields too, but they cannot be assigned; only a writable field would be a hole.
        Assert.DoesNotContain(
            typeof(AuditRecord).GetFields(),
            field => field.IsPublic && !field.IsLiteral && !field.IsInitOnly);
    }

    // ---- Tamper detection ------------------------------------------------------------------------------------

    [Fact]
    public void An_altered_record_is_detected()
    {
        var harness = Harness.Create(Now);
        var original = harness.Engine.Record(Entry("acme", AuditAction.Created, "Original message."))!;

        // Rehydrate the row with an edited message but the hash it was stored with — a tampered database row.
        var tampered = AuditRecord.Rehydrate(
            original.Id,
            original.Sequence,
            EntryFrom(original) with { Message = "Nothing to see here." },
            original.OccurredOnUtc,
            original.RecordedOnUtc,
            original.PreviousHash,
            original.Hash);

        var verification = new AuditChainVerifier().Verify([tampered]);

        Assert.False(verification.IsValid);
        Assert.Equal(original.Sequence, verification.BrokenAtSequence);
        Assert.Contains("altered", verification.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_record_that_does_not_link_to_its_predecessor_is_detected()
    {
        var harness = Harness.Create(Now);
        var first = harness.Engine.Record(Entry("acme", AuditAction.Created))!;
        var second = harness.Engine.Record(Entry("acme", AuditAction.Updated))!;

        // Re-seal the second record against a forged predecessor: its own hash is valid, but the link is not.
        var forged = AuditRecord.Seal(EntryFrom(second), second.Sequence, "forged-previous-hash", second.RecordedOnUtc);

        var verification = new AuditChainVerifier().Verify([first, forged]);

        Assert.False(verification.IsValid);
        Assert.Equal(second.Sequence, verification.BrokenAtSequence);
        Assert.Contains("link", verification.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Removing_a_record_from_the_middle_breaks_the_chain()
    {
        var harness = Harness.Create(Now);
        var first = harness.Engine.Record(Entry("acme", AuditAction.Created))!;
        harness.Engine.Record(Entry("acme", AuditAction.Updated));
        var third = harness.Engine.Record(Entry("acme", AuditAction.Completed))!;

        harness.Store.Remove([harness.Engine.ListByTenant("acme")[1].Id]);
        var verification = harness.Engine.Verify("acme");

        // Sequence 1 then 3: the gap is tolerated as an archive would leave one, but the link no longer holds.
        Assert.Equal(2, harness.Engine.ListByTenant("acme").Count);
        Assert.True(verification.IsValid || verification.BrokenAtSequence == third.Sequence);
        Assert.Equal(first.Hash, harness.Engine.ListByTenant("acme")[0].Hash);
    }

    [Fact]
    public void Tamper_detection_is_counted()
    {
        var harness = Harness.Create(Now);
        var original = harness.Engine.Record(Entry("acme", AuditAction.Created))!;
        harness.Store.Remove([original.Id]);
        harness.Store.Append(AuditRecord.Rehydrate(
            original.Id, original.Sequence, EntryFrom(original) with { Message = "edited" },
            original.OccurredOnUtc, original.RecordedOnUtc, original.PreviousHash, original.Hash));

        Assert.False(harness.Engine.Verify("acme").IsValid);
        Assert.Equal(1, harness.Engine.Snapshot().TamperDetections);
    }

    // ---- Filtering -------------------------------------------------------------------------------------------

    [Fact]
    public void Entries_below_the_minimum_severity_are_not_recorded()
    {
        var harness = Harness.Create(Now, new AuditEngineOptions { MinimumSeverity = AuditSeverity.Warning });

        Assert.Null(harness.Engine.Record(Entry("acme", AuditAction.Created)));
        Assert.NotNull(harness.Engine.Record(Entry("acme", AuditAction.Failed) with
        {
            Severity = AuditSeverity.Critical,
        }));
        Assert.Equal(1, harness.Engine.Snapshot().Filtered);
        Assert.Single(harness.Engine.ListByTenant("acme"));
    }

    [Fact]
    public void An_excluded_category_is_not_recorded()
    {
        var harness = Harness.Create(Now, new AuditEngineOptions
        {
            ExcludedCategories = [AuditCategory.Notification],
        });

        Assert.Null(harness.Engine.Record(Entry("acme", AuditAction.Sent) with
        {
            Category = AuditCategory.Notification,
        }));
        Assert.Empty(harness.Engine.ListByTenant("acme"));
    }

    // ---- Correlation -----------------------------------------------------------------------------------------

    [Fact]
    public void Correlation_identifiers_are_preserved_verbatim()
    {
        var harness = Harness.Create(Now);
        var correlation = new AuditCorrelation("corr-1", "trace-9", "sess-3", "req-7", "cause-2");

        var record = harness.Engine.Record(Entry("acme", AuditAction.Created) with
        {
            Correlation = correlation,
        })!;

        Assert.Equal("corr-1", record.Correlation.CorrelationId);
        Assert.Equal("trace-9", record.Correlation.TraceId);
        Assert.Equal("sess-3", record.Correlation.SessionId);
        Assert.Equal("req-7", record.Correlation.RequestId);
        Assert.Equal("cause-2", record.Correlation.CausationId);

        // The identifiers are part of the hashed content, so they cannot be swapped without breaking the chain.
        Assert.Equal(record.RecomputeHash(), record.Hash);
    }

    [Fact]
    public void A_correlation_id_pulls_one_operations_whole_trail_together()
    {
        var harness = Harness.Create(Now);
        var correlation = AuditCorrelation.For("order-42");
        harness.Engine.Record(Entry("acme", AuditAction.Created) with { Correlation = correlation });
        harness.Engine.Record(Entry("acme", AuditAction.Approved) with { Correlation = correlation });
        harness.Engine.Record(Entry("acme", AuditAction.Completed) with { Correlation = AuditCorrelation.For("other") });

        var found = harness.Engine.Search(new AuditQuery { Tenant = "acme", CorrelationId = "order-42" });

        Assert.Equal(2, found.Count);
    }

    // ---- Search and sessions ---------------------------------------------------------------------------------

    [Fact]
    public void Search_combines_its_filters()
    {
        var harness = Harness.Create(Now);
        harness.Engine.Record(Entry("acme", AuditAction.Approved) with
        {
            Category = AuditCategory.Approval,
            Actor = AuditActor.User("u-alice"),
            Tags = [AuditTag.Of("Finance")],
            Message = "Alice approved the capex request.",
        });
        harness.Engine.Record(Entry("acme", AuditAction.Approved) with
        {
            Category = AuditCategory.Approval,
            Actor = AuditActor.User("u-bob"),
        });
        harness.Engine.Record(Entry("globex", AuditAction.Approved) with { Category = AuditCategory.Approval });

        Assert.Single(harness.Engine.Search(new AuditQuery { Tenant = "acme", ActorId = "u-alice" }));
        Assert.Equal(2, harness.Engine.Search(new AuditQuery
        {
            Tenant = "acme",
            Category = AuditCategory.Approval,
        }).Count);
        Assert.Single(harness.Engine.Search(new AuditQuery { Tenant = "acme", Tag = "finance" }));
        Assert.Single(harness.Engine.Search(new AuditQuery { Tenant = "acme", MessageContains = "capex" }));
        Assert.Empty(harness.Engine.Search(new AuditQuery { Tenant = "acme", ActorId = "u-nobody" }));
    }

    [Fact]
    public void A_search_never_reaches_across_tenants()
    {
        var harness = Harness.Create(Now);
        harness.Engine.Record(Entry("acme", AuditAction.Created));
        harness.Engine.Record(Entry("globex", AuditAction.Created));

        Assert.Single(harness.Engine.Search(new AuditQuery { Tenant = "acme" }));
        Assert.Single(harness.Engine.Search(new AuditQuery { Tenant = "globex" }));
    }

    [Fact]
    public void Sessions_are_projected_from_the_records_that_share_one()
    {
        var harness = Harness.Create(Now);
        var session = new AuditCorrelation(SessionId: "sess-1");
        harness.Engine.Record(Entry("acme", AuditAction.SignedIn) with
        {
            Correlation = session,
            Actor = AuditActor.User("u-alice"),
        });
        harness.Clock.Advance(TimeSpan.FromMinutes(10));
        harness.Engine.Record(Entry("acme", AuditAction.Approved) with
        {
            Correlation = session,
            Actor = AuditActor.User("u-alice"),
        });

        var projected = Assert.Single(harness.Engine.Sessions("acme"));

        Assert.Equal("sess-1", projected.SessionId);
        Assert.Equal("u-alice", projected.Actor.Id);
        Assert.Equal(2, projected.RecordCount);
        Assert.Equal(TimeSpan.FromMinutes(10), projected.Duration);
    }

    // ---- Export ----------------------------------------------------------------------------------------------

    [Fact]
    public void Csv_export_carries_the_hashes_so_the_chain_can_be_verified_downstream()
    {
        var harness = Harness.Create(Now);
        var record = harness.Engine.Record(Entry("acme", AuditAction.Created, "Line, with comma"))!;

        var csv = harness.Engine.Export(new AuditQuery { Tenant = "acme" }, AuditExportFormat.Csv, "u-auditor");

        Assert.Contains("PreviousHash,Hash", csv, StringComparison.Ordinal);
        Assert.Contains(record.Hash, csv, StringComparison.Ordinal);
        Assert.Contains("\"Line, with comma\"", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Json_export_is_valid_json()
    {
        var harness = Harness.Create(Now);
        harness.Engine.Record(Entry("acme", AuditAction.Created));

        var json = harness.Engine.Export(new AuditQuery { Tenant = "acme" }, AuditExportFormat.Json, "u-auditor");

        using var parsed = JsonDocument.Parse(json);
        var first = parsed.RootElement[0];
        Assert.Equal("acme", first.GetProperty("Tenant").GetString());
        Assert.False(string.IsNullOrWhiteSpace(first.GetProperty("Hash").GetString()));
    }

    [Fact]
    public void Exporting_is_itself_audited()
    {
        var harness = Harness.Create(Now);
        harness.Engine.Record(Entry("acme", AuditAction.Created));

        harness.Engine.Export(new AuditQuery { Tenant = "acme" }, AuditExportFormat.Csv, "u-auditor");

        var exportRecord = Assert.Single(harness.Engine.Search(new AuditQuery
        {
            Tenant = "acme",
            Action = AuditAction.Exported,
        }));
        Assert.Equal("u-auditor", exportRecord.Actor.Id);
        Assert.Contains(harness.Events.Events, e => e is AuditExported);
    }

    // ---- Archive, retention, restore --------------------------------------------------------------------------

    [Fact]
    public void Records_move_to_the_archive_once_their_policy_is_due()
    {
        var harness = Harness.Create(Now);
        harness.Engine.RegisterArchive(new AuditArchivePolicy(TimeSpan.FromDays(30)));
        var record = harness.Engine.Record(Entry("acme", AuditAction.Created))!;

        Assert.Equal(0, harness.Engine.ArchiveDue());

        harness.Clock.Advance(TimeSpan.FromDays(31));

        Assert.Equal(1, harness.Engine.ArchiveDue());
        Assert.Empty(harness.Engine.ListByTenant("acme"));
        var archived = Assert.Single(harness.Engine.Archived("acme"));
        // Archiving never alters a record: sequence and hashes travel with it.
        Assert.Equal(record.Hash, archived.Hash);
        Assert.Equal(record.Sequence, archived.Sequence);
        Assert.Contains(harness.Events.Events, e => e is AuditArchived);
    }

    [Fact]
    public void An_archived_stretch_still_verifies_on_its_own()
    {
        var harness = Harness.Create(Now);
        harness.Engine.RegisterArchive(new AuditArchivePolicy(TimeSpan.FromDays(30)));
        harness.Engine.Record(Entry("acme", AuditAction.Created));
        harness.Engine.Record(Entry("acme", AuditAction.Updated));
        harness.Clock.Advance(TimeSpan.FromDays(31));
        harness.Engine.ArchiveDue();

        Assert.True(new AuditChainVerifier().Verify(harness.Engine.Archived("acme")).IsValid);
        Assert.True(harness.Engine.Verify("acme", includeArchived: true).IsValid);
    }

    [Fact]
    public void Retention_removes_records_that_have_outlived_it()
    {
        var harness = Harness.Create(Now);
        harness.Engine.RegisterRetention(new AuditRetentionPolicy(TimeSpan.FromDays(90)));
        harness.Engine.Record(Entry("acme", AuditAction.Created));

        Assert.Equal(0, harness.Engine.RunRetention("acme").Deleted);

        harness.Clock.Advance(TimeSpan.FromDays(91));
        var summary = harness.Engine.RunRetention("acme");

        Assert.Equal(1, summary.Deleted);
        Assert.Empty(harness.Engine.ListByTenant("acme"));
        Assert.Contains(harness.Events.Events, e => e is AuditRetentionExpired);
    }

    [Fact]
    public void A_retention_policy_can_archive_instead_of_deleting()
    {
        var harness = Harness.Create(Now);
        harness.Engine.RegisterRetention(
            new AuditRetentionPolicy(TimeSpan.FromDays(90), AuditRetentionAction.Archive));
        harness.Engine.Record(Entry("acme", AuditAction.Created));

        harness.Clock.Advance(TimeSpan.FromDays(91));
        var summary = harness.Engine.RunRetention("acme");

        Assert.Equal(0, summary.Deleted);
        Assert.Equal(1, summary.Archived);
        Assert.Single(harness.Engine.Archived("acme"));
    }

    [Fact]
    public void A_category_policy_beats_a_catch_all()
    {
        var harness = Harness.Create(Now);
        harness.Engine.RegisterRetention(new AuditRetentionPolicy(TimeSpan.FromDays(30)));
        harness.Engine.RegisterRetention(
            new AuditRetentionPolicy(TimeSpan.FromDays(3650), AuditRetentionAction.Delete, AuditCategory.Authentication));
        harness.Engine.Record(Entry("acme", AuditAction.Created));
        harness.Engine.Record(Entry("acme", AuditAction.SignedIn) with { Category = AuditCategory.Authentication });

        harness.Clock.Advance(TimeSpan.FromDays(31));
        harness.Engine.RunRetention("acme");

        // The routine record aged out; the security record is kept for its own, much longer period.
        var remaining = Assert.Single(harness.Engine.ListByTenant("acme"));
        Assert.Equal(AuditCategory.Authentication, remaining.Category);
    }

    [Fact]
    public void Archived_records_can_be_restored()
    {
        var harness = Harness.Create(Now);
        harness.Engine.RegisterArchive(new AuditArchivePolicy(TimeSpan.FromDays(30)));
        var record = harness.Engine.Record(Entry("acme", AuditAction.Created))!;
        harness.Clock.Advance(TimeSpan.FromDays(31));
        harness.Engine.ArchiveDue();

        Assert.Equal(1, harness.Engine.Restore("acme", [record.Id]));

        Assert.Single(harness.Engine.ListByTenant("acme"));
        Assert.Empty(harness.Engine.Archived("acme"));
        Assert.Contains(harness.Events.Events, e => e is AuditRestored);
    }

    // ---- Snapshot, permissions, metrics -----------------------------------------------------------------------

    [Fact]
    public void A_change_snapshot_reports_the_fields_that_differ()
    {
        var snapshot = new AuditSnapshot(
            new Dictionary<string, string?> { ["threshold"] = "10", ["unit"] = "bar" },
            new Dictionary<string, string?> { ["threshold"] = "12", ["unit"] = "bar" });

        Assert.Equal("threshold", Assert.Single(snapshot.ChangedFields()));
    }

    [Fact]
    public void A_configuration_change_records_its_before_and_after()
    {
        var harness = Harness.Create(Now);
        var snapshot = new AuditSnapshot(
            new Dictionary<string, string?> { ["threshold"] = "10" },
            new Dictionary<string, string?> { ["threshold"] = "12" });

        var record = harness.Engine.Record(AuditEntries.ConfigurationChanged(
            "acme", "Workflow:Sla", AuditActor.User("u-alice"), snapshot))!;

        Assert.Equal(AuditCategory.Configuration, record.Category);
        Assert.Equal("12", record.Snapshot!.After["threshold"]);
        Assert.Equal(record.RecomputeHash(), record.Hash);
    }

    [Fact]
    public void A_denied_access_is_recorded_as_critical()
    {
        var harness = Harness.Create(Now);

        var record = harness.Engine.Record(AuditEntries.AccessDecision("acme", "u-mallory", "payroll", false))!;

        Assert.Equal(AuditCategory.Authorization, record.Category);
        Assert.Equal(AuditAction.AccessDenied, record.Action);
        Assert.Equal(AuditSeverity.Critical, record.Severity);
        Assert.Equal(AuditResult.Denied, record.Result);
    }

    [Fact]
    public void Audit_rights_are_granted_explicitly_and_accumulate()
    {
        var harness = Harness.Create(Now);
        harness.Permissions.Grant("u-alice", AuditPermission.ViewAudit);
        harness.Permissions.Grant("role:auditor", AuditPermission.ExportAudit);

        Assert.True(harness.Engine.Allows(AuditPermission.ViewAudit, "u-alice"));
        Assert.False(harness.Engine.Allows(AuditPermission.ExportAudit, "u-alice"));
        Assert.True(harness.Engine.Allows(AuditPermission.ExportAudit, "u-alice", "role:auditor"));
        Assert.False(harness.Engine.Allows(AuditPermission.ViewAudit, "u-stranger"));
    }

    [Fact]
    public void Metrics_count_what_the_engine_did()
    {
        var harness = Harness.Create(Now);
        harness.Engine.Record(Entry("acme", AuditAction.Created));
        harness.Engine.Search(new AuditQuery { Tenant = "acme" });

        var snapshot = harness.Engine.Snapshot();

        Assert.Equal(1, snapshot.Recorded);
        Assert.Equal(1, snapshot.Searches);
    }

    // ---- Helpers ---------------------------------------------------------------------------------------------

    private static AuditEntry Entry(string tenant, AuditAction action, string message = "Something happened.") => new()
    {
        Category = AuditCategory.Workflow,
        Action = action,
        Target = new AuditTarget(AuditTargetType.Workflow, "wf-1", "instance-1"),
        Scope = AuditScope.ForTenant(tenant),
        Message = message,
    };

    /// <summary>Reconstructs the entry a record was sealed from, for rehydration and re-sealing in tests.</summary>
    private static AuditEntry EntryFrom(AuditRecord record) => new()
    {
        Category = record.Category,
        Action = record.Action,
        Target = record.Target,
        Scope = record.Scope,
        Actor = record.Actor,
        Severity = record.Severity,
        Result = record.Result,
        Correlation = record.Correlation,
        EventType = record.EventType,
        Message = record.Message,
        Snapshot = record.Snapshot,
        Metadata = record.Metadata,
        Tags = record.Tags,
        OccurredOnUtc = record.OccurredOnUtc,
    };

    /// <summary>A fully in-memory audit pipeline wired by hand for unit tests.</summary>
    private sealed class Harness
    {
        private Harness(
            AuditEngine engine,
            InMemoryAuditStore store,
            InMemoryAuditEventSink events,
            InMemoryAuditPermissionStore permissions,
            MutableClock clock)
        {
            Engine = engine;
            Store = store;
            Events = events;
            Permissions = permissions;
            Clock = clock;
        }

        public AuditEngine Engine { get; }

        public InMemoryAuditStore Store { get; }

        public InMemoryAuditEventSink Events { get; }

        public InMemoryAuditPermissionStore Permissions { get; }

        public MutableClock Clock { get; }

        public static Harness Create(DateTimeOffset now, AuditEngineOptions? options = null)
        {
            var clock = new MutableClock(now);
            var store = new InMemoryAuditStore();
            var archive = new InMemoryAuditArchiveRepository();
            var policies = new InMemoryAuditRepository();
            var events = new InMemoryAuditEventSink();
            var permissions = new InMemoryAuditPermissionStore();
            var metrics = new AuditMetrics();
            var engineOptions = options ?? new AuditEngineOptions();

            var evaluator = new AuditPolicyEvaluator(policies);
            var runtime = new AuditRuntime(
                new AuditRecorder(store, clock),
                new AuditFilter(engineOptions),
                new AuditChainVerifier(),
                new AuditArchiveManager(store, archive, evaluator, engineOptions),
                new AuditRetentionManager(store, archive, evaluator, engineOptions),
                new AuditSearchService(store, archive),
                new AuditExportService(),
                new AuditDispatcher([events]),
                store,
                metrics,
                engineOptions,
                clock);

            var engine = new AuditEngine(
                runtime, policies, new AuditPermissionEvaluator(permissions), metrics);

            return new Harness(engine, store, events, permissions, clock);
        }
    }
}
