using FactoryOS.Plugins.Workflow.Monitoring.Collections;
using FactoryOS.Plugins.Workflow.Monitoring.Diagnostics;
using FactoryOS.Plugins.Workflow.Monitoring.Configuration;
using FactoryOS.Plugins.Workflow.Monitoring.Domain;
using FactoryOS.Plugins.Workflow.Monitoring.Events;
using FactoryOS.Plugins.Workflow.Monitoring.Execution;
using FactoryOS.Plugins.Workflow.Monitoring.Health;
using FactoryOS.Plugins.Workflow.Monitoring.Persistence;
using FactoryOS.Tests.Identity;

namespace FactoryOS.Tests.Workflow.Monitoring;

/// <summary>
/// Unit coverage of the monitoring engine core: collecting measurements, sampling, aggregation, retention and
/// roll-up, thresholds, alerts, health checks, search and correlation — exercised directly, without a container
/// and without any of the engines whose events it measures.
/// </summary>
public sealed class MonitoringEngineCoreTests
{
    private const string Tenant = "acme";
    private static readonly DateTimeOffset Now = new(2026, 07, 22, 09, 00, 00, TimeSpan.Zero);

    // ---- Metric collection -------------------------------------------------------------------------------------

    [Fact]
    public void A_measurement_lands_in_the_series_its_labels_identify()
    {
        var harness = Harness.Create(Now);

        harness.Engine.Count(Tenant, WorkflowMetricCollection.InstancesStarted, Key("order-approval"));
        harness.Engine.Count(Tenant, WorkflowMetricCollection.InstancesStarted, Key("stock-count"));
        harness.Engine.Count(Tenant, WorkflowMetricCollection.InstancesStarted, Key("order-approval"));

        var orders = harness.Store.Find(
            MetricInstance.Of(Tenant, WorkflowMetricCollection.InstancesStarted, Key("order-approval")))!;
        var stock = harness.Store.Find(
            MetricInstance.Of(Tenant, WorkflowMetricCollection.InstancesStarted, Key("stock-count")))!;

        Assert.Equal(2, orders.Count);
        Assert.Equal(1, stock.Count);
    }

    [Fact]
    public void Labels_supplied_in_a_different_order_still_mean_the_same_series()
    {
        var harness = Harness.Create(Now);
        var forwards = MetricDimension.Of(MetricLabel.Of("key", "k"), MetricLabel.Of("outcome", "Approved"));
        var backwards = MetricDimension.Of(MetricLabel.Of("outcome", "Approved"), MetricLabel.Of("key", "k"));

        harness.Engine.Count(Tenant, ApprovalMetricCollection.Completed, forwards);
        harness.Engine.Count(Tenant, ApprovalMetricCollection.Completed, backwards);

        // One series, not two: identity is the canonical rendering, not the order the caller happened to use.
        Assert.Single(harness.Store.ListByMetric(Tenant, ApprovalMetricCollection.Completed));
        Assert.Equal(forwards, backwards);
    }

    [Fact]
    public void Every_tenant_measures_into_its_own_series()
    {
        var harness = Harness.Create(Now);

        harness.Engine.Count(Tenant, WorkflowMetricCollection.InstancesStarted, Key("k"));
        harness.Engine.Count("globex", WorkflowMetricCollection.InstancesStarted, Key("k"));

        Assert.Single(harness.Store.ListByTenant(Tenant));
        Assert.Single(harness.Store.ListByTenant("globex"));
        Assert.Equal(1, harness.Store.ListByTenant(Tenant)[0].Count);
    }

    [Fact]
    public void An_unregistered_metric_is_refused_rather_than_given_a_series_of_its_own()
    {
        var harness = Harness.Create(Now);

        // A metric nobody defined is a typo far more often than it is a new measurement.
        var error = Assert.Throws<InvalidOperationException>(
            () => harness.Engine.Count(Tenant, "workflow.instance.startd", Key("k")));
        Assert.Contains("not registered", error.Message, StringComparison.Ordinal);
    }

    // ---- Sampling ----------------------------------------------------------------------------------------------

    [Fact]
    public void Sampling_keeps_one_measurement_in_every_interval()
    {
        var harness = Harness.Create(Now);
        harness.Repository.Register(new MetricDefinition(
            "test.sampled.gauge", MetricCategory.Performance, MetricKind.Gauge, "count", "A sampled gauge.")
        {
            SampleRate = 0.25,
        });

        var recorded = 0;
        for (var index = 0; index < 12; index++)
        {
            if (harness.Engine.Record(Tenant, "test.sampled.gauge", index).WasRecorded)
            {
                recorded++;
            }
        }

        Assert.Equal(3, recorded);
    }

    [Fact]
    public void A_sampled_counter_still_reports_the_true_total()
    {
        var harness = Harness.Create(Now);
        harness.Repository.Register(new MetricDefinition(
            "test.sampled.counter", MetricCategory.Performance, MetricKind.Counter, "count", "A sampled counter.")
        {
            SampleRate = 0.1,
        });

        for (var index = 0; index < 100; index++)
        {
            harness.Engine.Count(Tenant, "test.sampled.counter");
        }

        // Ten measurements were kept, each standing for the ten it represents. Sampling costs resolution,
        // never correctness — a total that read 10 here would understate the truth by ninety percent.
        var snapshot = harness.Engine.Snapshot(
            MetricInstance.Of(Tenant, "test.sampled.counter"), MetricAggregation.Sum, TimeSpan.FromHours(1));
        Assert.Equal(100, snapshot.Value);
        Assert.Equal(100, snapshot.Count);
    }

    [Fact]
    public void A_sample_rate_that_would_record_nothing_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MetricDefinition(
            "test.zero", MetricCategory.Performance, MetricKind.Counter, "count", "Never recorded.")
        {
            SampleRate = 0,
        });
    }

    // ---- Aggregation -------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(MetricAggregation.Sum, 60)]
    [InlineData(MetricAggregation.Count, 4)]
    [InlineData(MetricAggregation.Average, 15)]
    [InlineData(MetricAggregation.Minimum, 5)]
    [InlineData(MetricAggregation.Maximum, 30)]
    [InlineData(MetricAggregation.Last, 15)]
    public void Every_aggregation_collapses_a_window_the_way_it_says_it_does(
        MetricAggregation aggregation, double expected)
    {
        var harness = Harness.Create(Now);
        foreach (var value in new[] { 10d, 5d, 30d, 15d })
        {
            harness.Engine.Record(Tenant, PerformanceMetricCollection.OperationDuration, value, Key("op"));
            harness.Clock.Advance(TimeSpan.FromSeconds(10));
        }

        var snapshot = harness.Engine.Snapshot(
            MetricInstance.Of(Tenant, PerformanceMetricCollection.OperationDuration, Key("op")),
            aggregation,
            TimeSpan.FromHours(1));

        Assert.Equal(expected, snapshot.Value);
    }

    [Fact]
    public void A_rate_is_the_total_spread_over_the_window_it_was_measured_in()
    {
        var harness = Harness.Create(Now);
        for (var index = 0; index < 120; index++)
        {
            harness.Engine.Count(Tenant, ApiMetricCollection.Requests, Key("orders"));
        }

        var snapshot = harness.Engine.Snapshot(
            MetricInstance.Of(Tenant, ApiMetricCollection.Requests, Key("orders")),
            MetricAggregation.Rate,
            TimeSpan.FromMinutes(1));

        Assert.Equal(2, snapshot.Value);
    }

    [Fact]
    public void A_percentile_reports_a_number_that_was_actually_measured()
    {
        var harness = Harness.Create(Now);
        for (var index = 1; index <= 100; index++)
        {
            harness.Engine.Record(Tenant, ApiMetricCollection.Latency, index, Key("orders"));
        }

        var snapshot = harness.Engine.Snapshot(
            MetricInstance.Of(Tenant, ApiMetricCollection.Latency, Key("orders")),
            MetricAggregation.Percentile95,
            TimeSpan.FromHours(1));

        // Nearest-rank, so "the 95th percentile was 95 ms" names a request somebody can go and find.
        Assert.Equal(95, snapshot.Value);
    }

    [Fact]
    public void An_empty_window_is_reported_as_empty_rather_than_as_zero()
    {
        var harness = Harness.Create(Now);
        harness.Engine.Count(Tenant, WorkflowMetricCollection.InstancesStarted, Key("k"));
        harness.Clock.Advance(TimeSpan.FromHours(2));

        var snapshot = harness.Engine.Snapshot(
            MetricInstance.Of(Tenant, WorkflowMetricCollection.InstancesStarted, Key("k")),
            MetricAggregation.Sum,
            TimeSpan.FromMinutes(5));

        Assert.True(snapshot.IsEmpty);
        Assert.Equal(0, snapshot.Count);
    }

    // ---- Retention ---------------------------------------------------------------------------------------------

    [Fact]
    public void Retention_drops_measurements_that_outlived_their_policy()
    {
        var harness = Harness.Create(Now);
        harness.Engine.RegisterRetention(new MetricRetentionPolicy(
            TimeSpan.FromMinutes(30), MetricRetentionAction.Delete));

        harness.Engine.Count(Tenant, WorkflowMetricCollection.InstancesStarted, Key("k"));
        harness.Clock.Advance(TimeSpan.FromHours(1));
        harness.Engine.Count(Tenant, WorkflowMetricCollection.InstancesStarted, Key("k"));

        var summary = harness.Engine.RunRetention(Tenant);

        Assert.Equal(1, summary.Removed);
        Assert.Equal(1, harness.Store.ListByTenant(Tenant)[0].Count);
    }

    [Fact]
    public void A_metric_with_no_policy_still_has_a_ceiling()
    {
        var harness = Harness.Create(Now, new MonitoringEngineOptions { DefaultRetention = TimeSpan.FromHours(1) });

        harness.Engine.Count(Tenant, WorkflowMetricCollection.InstancesStarted, Key("k"));
        harness.Clock.Advance(TimeSpan.FromHours(2));

        // No policy was registered at all; a store with no ceiling is a memory leak with a dashboard attached.
        Assert.Equal(1, harness.Engine.RunRetention(Tenant).Removed);
    }

    [Fact]
    public void A_roll_up_keeps_the_shape_of_old_history_without_keeping_every_point_of_it()
    {
        var harness = Harness.Create(Now);
        harness.Engine.RegisterRetention(new MetricRetentionPolicy(
            TimeSpan.FromHours(3), MetricRetentionAction.RollUp)
        {
            MetricKey = WorkflowMetricCollection.InstancesCompleted,
            RollUpBucket = TimeSpan.FromHours(1),
            RollUpUsing = MetricAggregation.Sum,
        });

        // Twelve measurements spread over four hours, then a pass that only ages out the first three hours.
        for (var index = 0; index < 12; index++)
        {
            harness.Engine.Count(Tenant, WorkflowMetricCollection.InstancesCompleted, Key("k"));
            harness.Clock.Advance(TimeSpan.FromMinutes(20));
        }

        var summary = harness.Engine.RunRetention(Tenant);
        var series = harness.Store.ListByTenant(Tenant)[0];

        Assert.True(summary.RolledUp > 0);
        Assert.True(series.Count < 12);

        // The total survives the roll-up: the rolled-up value carries the weight of everything it replaced.
        var snapshot = harness.Engine.Snapshot(
            series.Instance, MetricAggregation.Sum, TimeSpan.FromDays(1));
        Assert.Equal(12, snapshot.Value);
    }

    [Fact]
    public void Retention_announces_what_it_removed()
    {
        var harness = Harness.Create(Now);
        harness.Engine.RegisterRetention(new MetricRetentionPolicy(
            TimeSpan.FromMinutes(1), MetricRetentionAction.Delete));
        harness.Engine.Count(Tenant, WorkflowMetricCollection.InstancesStarted, Key("k"));
        harness.Clock.Advance(TimeSpan.FromHours(1));

        harness.Engine.RunRetention(Tenant);

        var expired = Assert.Single(harness.Events.Events.OfType<MetricRetentionExpired>());
        Assert.Equal(1, expired.Removed);
    }

    // ---- Thresholds --------------------------------------------------------------------------------------------

    [Fact]
    public void A_threshold_puts_a_metric_in_the_state_its_limits_describe()
    {
        var harness = Harness.Create(Now);
        harness.Engine.RegisterThreshold(new MetricThreshold(
            "failures", WorkflowMetricCollection.InstancesFailed, MetricComparison.GreaterThan, 5)
        {
            WarningAt = 2,
            Window = TimeSpan.FromMinutes(10),
        });

        harness.Engine.Count(Tenant, WorkflowMetricCollection.InstancesFailed, Key("k"));
        harness.Engine.Count(Tenant, WorkflowMetricCollection.InstancesFailed, Key("k"));
        harness.Engine.Count(Tenant, WorkflowMetricCollection.InstancesFailed, Key("k"));

        var warning = Assert.Single(harness.Engine.Evaluate(Tenant));
        Assert.Equal(MetricHealthState.Warning, warning.State);

        for (var index = 0; index < 5; index++)
        {
            harness.Engine.Count(Tenant, WorkflowMetricCollection.InstancesFailed, Key("k"));
        }

        Assert.Equal(MetricHealthState.Critical, harness.Engine.Evaluate(Tenant).Single().State);
    }

    [Fact]
    public void A_threshold_judges_each_slice_of_a_metric_on_its_own()
    {
        var harness = Harness.Create(Now);
        harness.Engine.RegisterThreshold(new MetricThreshold(
            "deadletters", NotificationMetricCollection.DeadLettered, MetricComparison.GreaterThan, 2));

        var email = MetricDimension.Of(MetricLabel.Of("channel", "Email"));
        var sms = MetricDimension.Of(MetricLabel.Of("channel", "Sms"));
        for (var index = 0; index < 5; index++)
        {
            harness.Engine.Count(Tenant, NotificationMetricCollection.DeadLettered, email);
        }

        harness.Engine.Count(Tenant, NotificationMetricCollection.DeadLettered, sms);

        // One collapsed channel must be visible even while the others are fine — averaging would hide it.
        var evaluations = harness.Engine.Evaluate(Tenant);
        Assert.Equal(2, evaluations.Count);
        Assert.Single(evaluations, evaluation => evaluation.State == MetricHealthState.Critical);
        Assert.Single(evaluations, evaluation => evaluation.State == MetricHealthState.Ok);
    }

    [Fact]
    public void A_window_with_no_measurements_is_unknown_rather_than_within_limits()
    {
        var harness = Harness.Create(Now);
        harness.Engine.RegisterThreshold(new MetricThreshold(
            "failures", WorkflowMetricCollection.InstancesFailed, MetricComparison.GreaterThan, 1)
        {
            Window = TimeSpan.FromMinutes(5),
        });

        harness.Engine.Count(Tenant, WorkflowMetricCollection.InstancesFailed, Key("k"));
        harness.Clock.Advance(TimeSpan.FromHours(1));

        // No traffic is not evidence of health.
        Assert.Equal(MetricHealthState.Unknown, harness.Engine.Evaluate(Tenant).Single().State);
    }

    [Fact]
    public void A_breach_is_announced_with_the_operation_that_caused_it()
    {
        var harness = Harness.Create(Now);
        harness.Engine.RegisterThreshold(new MetricThreshold(
            "failures", WorkflowMetricCollection.InstancesFailed, MetricComparison.GreaterThan, 0));

        harness.Engine.Count(
            Tenant, WorkflowMetricCollection.InstancesFailed, Key("k"), MetricCorrelation.For("run-42"));
        harness.Engine.Evaluate(Tenant);

        var exceeded = Assert.Single(harness.Events.Events.OfType<ThresholdExceeded>());
        Assert.Equal("run-42", exceeded.Correlation.CorrelationId);
        Assert.Equal(0, exceeded.Limit);
    }

    // ---- Alerts ------------------------------------------------------------------------------------------------

    [Fact]
    public void An_alert_waits_out_its_delay_before_it_opens()
    {
        var harness = Harness.Create(Now);
        RegisterFailureAlert(harness, TimeSpan.FromMinutes(5));

        harness.Engine.Count(Tenant, WorkflowMetricCollection.InstancesFailed, Key("k"));
        harness.Engine.Evaluate(Tenant);

        // A single spike pages nobody.
        Assert.Empty(harness.Engine.OpenAlerts(Tenant));

        harness.Clock.Advance(TimeSpan.FromMinutes(6));
        harness.Engine.Count(Tenant, WorkflowMetricCollection.InstancesFailed, Key("k"));
        harness.Engine.Evaluate(Tenant);

        var alert = Assert.Single(harness.Engine.OpenAlerts(Tenant));
        Assert.True(alert.IsOpen);
        Assert.Single(harness.Events.Events.OfType<AlertTriggered>());
    }

    [Fact]
    public void An_alert_closes_when_the_metric_comes_back_inside_its_limits()
    {
        var harness = Harness.Create(Now);
        RegisterFailureAlert(harness, TimeSpan.Zero);

        harness.Engine.Count(Tenant, WorkflowMetricCollection.InstancesFailed, Key("k"));
        harness.Engine.Evaluate(Tenant);
        Assert.Single(harness.Engine.OpenAlerts(Tenant));

        // The failure ages out of the window while a fresh success keeps the series producing.
        harness.Clock.Advance(TimeSpan.FromMinutes(10));
        harness.Engine.Record(Tenant, WorkflowMetricCollection.InstancesFailed, 0, Key("k"));
        harness.Engine.Evaluate(Tenant);

        Assert.Empty(harness.Engine.OpenAlerts(Tenant));
        var resolved = Assert.Single(harness.Events.Events.OfType<AlertResolved>());
        Assert.Equal(TimeSpan.FromMinutes(10), resolved.OpenFor);
    }

    [Fact]
    public void A_metric_that_goes_silent_does_not_close_an_open_alert()
    {
        var harness = Harness.Create(Now);
        RegisterFailureAlert(harness, TimeSpan.Zero);

        harness.Engine.Count(Tenant, WorkflowMetricCollection.InstancesFailed, Key("k"));
        harness.Engine.Evaluate(Tenant);
        Assert.Single(harness.Engine.OpenAlerts(Tenant));

        // Nothing more is measured at all: the series has stopped producing, which more often means whatever
        // produced it died than that it recovered.
        harness.Clock.Advance(TimeSpan.FromHours(1));
        harness.Engine.Evaluate(Tenant);

        Assert.Single(harness.Engine.OpenAlerts(Tenant));
        Assert.Empty(harness.Events.Events.OfType<AlertResolved>());
    }

    [Fact]
    public void An_alert_opens_once_and_stays_open_while_the_breach_lasts()
    {
        var harness = Harness.Create(Now);
        RegisterFailureAlert(harness, TimeSpan.Zero);

        for (var index = 0; index < 3; index++)
        {
            harness.Engine.Count(Tenant, WorkflowMetricCollection.InstancesFailed, Key("k"));
            harness.Engine.Evaluate(Tenant);
        }

        Assert.Single(harness.Engine.OpenAlerts(Tenant));
        Assert.Single(harness.Events.Events.OfType<AlertTriggered>());
    }

    // ---- Health ------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_component_with_no_signal_is_unknown_rather_than_healthy()
    {
        var harness = Harness.Create(Now);

        var result = await harness.Engine.CheckAsync(Tenant, "workflow-engine");

        Assert.Equal(HealthStatus.Unknown, result.Status);
        Assert.Contains("no signal", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_component_is_judged_on_what_failed_against_what_it_attempted()
    {
        var harness = Harness.Create(Now);
        for (var index = 0; index < 9; index++)
        {
            harness.Engine.Count(Tenant, WorkflowMetricCollection.InstancesCompleted, Key("k"));
        }

        harness.Engine.Count(Tenant, WorkflowMetricCollection.InstancesFailed, Key("k"));

        var result = await harness.Engine.CheckAsync(Tenant, "workflow-engine");

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal("0.1", result.Data["failureRatio"]);
    }

    [Fact]
    public async Task A_failing_critical_component_takes_the_whole_report_down()
    {
        var harness = Harness.Create(Now);
        for (var index = 0; index < 10; index++)
        {
            harness.Engine.Count(Tenant, WorkflowMetricCollection.InstancesFailed, Key("k"));
        }

        var report = await harness.Engine.CheckHealthAsync(Tenant);

        Assert.Equal(HealthStatus.Unhealthy, report.Status);
        Assert.Contains(report.Failing(), result => result.Key == "workflow-engine");
    }

    [Fact]
    public async Task A_failing_non_critical_component_only_degrades_the_report()
    {
        var harness = Harness.Create(Now);
        for (var index = 0; index < 10; index++)
        {
            harness.Engine.Count(Tenant, HumanTaskMetricCollection.Expired, Key("k"));
        }

        var report = await harness.Engine.CheckHealthAsync(Tenant);

        Assert.Equal(HealthStatus.Degraded, report.Status);
    }

    [Fact]
    public async Task Only_a_change_of_status_is_announced()
    {
        var harness = Harness.Create(Now);
        harness.Engine.Count(Tenant, WorkflowMetricCollection.InstancesCompleted, Key("k"));

        await harness.Engine.CheckAsync(Tenant, "workflow-engine");
        await harness.Engine.CheckAsync(Tenant, "workflow-engine");

        Assert.Equal(2, harness.Events.Events.OfType<HealthCheckCompleted>().Count());
        Assert.Single(harness.Events.Events.OfType<HealthStatusChanged>());
    }

    [Fact]
    public async Task A_probe_that_throws_degrades_its_component_and_nothing_else()
    {
        var harness = Harness.Create(Now);
        harness.Engine.RegisterHealthCheck(
            new HealthCheck("exploding", "Exploding Probe", MetricCategory.Performance),
            (_, _) => throw new InvalidOperationException("the probe is broken"));

        var report = await harness.Engine.CheckHealthAsync(Tenant);

        // The layer that tells you whether the platform is up must not be able to take it down.
        var failed = Assert.Single(report.Results, result => result.Key == "exploding");
        Assert.Equal(HealthStatus.Unhealthy, failed.Status);
        Assert.Contains("the probe is broken", failed.Detail, StringComparison.Ordinal);
        Assert.Equal(13, report.Results.Count);
    }

    [Fact]
    public void The_platform_reports_on_twelve_components()
    {
        var harness = Harness.Create(Now);

        var checks = harness.Engine.HealthChecks();

        Assert.Equal(12, checks.Count);
        Assert.Contains(checks, check => check.Key == "workflow-engine" && check.IsCritical);
        Assert.Contains(checks, check => check.Key == "database" && check.IsCritical);
        Assert.Contains(checks, check => check.Key == "storage" && !check.IsCritical);
    }

    // ---- Search ------------------------------------------------------------------------------------------------

    [Fact]
    public void A_search_narrows_by_prefix_and_by_category()
    {
        var harness = Harness.Create(Now);
        harness.Engine.Count(Tenant, WorkflowMetricCollection.InstancesStarted, Key("a"));
        harness.Engine.Count(Tenant, WorkflowMetricCollection.InstancesCompleted, Key("a"));
        harness.Engine.Count(Tenant, HumanTaskMetricCollection.Created, Key("a"));

        var byPrefix = harness.Engine.Search(new MetricQuery(Tenant)
        {
            KeyPrefix = "workflow.",
            FromUtc = Now.AddHours(-1),
        });
        var byCategory = harness.Engine.Search(new MetricQuery(Tenant)
        {
            Category = MetricCategory.HumanTask,
            FromUtc = Now.AddHours(-1),
        });

        Assert.Equal(2, byPrefix.Count);
        Assert.Single(byCategory);
    }

    [Fact]
    public void A_search_never_reaches_across_tenants()
    {
        var harness = Harness.Create(Now);
        harness.Engine.Count(Tenant, WorkflowMetricCollection.InstancesStarted, Key("a"));
        harness.Engine.Count("globex", WorkflowMetricCollection.InstancesStarted, Key("a"));

        var found = harness.Engine.Search(new MetricQuery(Tenant) { FromUtc = Now.AddHours(-1) });

        Assert.All(found, snapshot => Assert.Equal(Tenant, snapshot.Instance.Tenant));
    }

    [Fact]
    public void A_search_can_narrow_to_one_slice_of_a_metric()
    {
        var harness = Harness.Create(Now);
        harness.Engine.Count(
            Tenant, NotificationMetricCollection.Sent,
            MetricDimension.Of(MetricLabel.Of("channel", "Email")));
        harness.Engine.Count(
            Tenant, NotificationMetricCollection.Sent,
            MetricDimension.Of(MetricLabel.Of("channel", "Sms")));

        var found = harness.Engine.Search(new MetricQuery(Tenant)
        {
            Dimension = MetricDimension.Of(MetricLabel.Of("channel", "Email")),
            FromUtc = Now.AddHours(-1),
        });

        Assert.Single(found);
        Assert.Equal("Email", Assert.Single(found).Instance.Dimension["channel"]);
    }

    // ---- Correlation -------------------------------------------------------------------------------------------

    [Fact]
    public void Every_measurement_an_operation_produced_can_be_found_from_its_correlation_id()
    {
        var harness = Harness.Create(Now);
        var correlation = new MetricCorrelation("op-7", "trace-9", "req-3");

        harness.Engine.Count(Tenant, WorkflowMetricCollection.InstancesStarted, Key("k"), correlation);
        harness.Engine.Count(Tenant, HumanTaskMetricCollection.Created, Key("k"), correlation);
        harness.Engine.Count(Tenant, WorkflowMetricCollection.InstancesStarted, Key("k"));

        var traced = harness.Engine.ByCorrelation(Tenant, "op-7");

        Assert.Equal(2, traced.Count);
        Assert.All(traced, pair => Assert.Equal("op-7", pair.Value.Correlation.CorrelationId));
    }

    [Fact]
    public void A_trace_id_and_a_request_id_survive_the_journey_into_the_store()
    {
        var harness = Harness.Create(Now);
        harness.Engine.Count(
            Tenant, ApiMetricCollection.Requests, Key("orders"),
            new MetricCorrelation("op-7", "trace-9", "req-3"));

        var pair = Assert.Single(harness.Engine.ByTrace(Tenant, "trace-9"));

        Assert.Equal("trace-9", pair.Value.Correlation.TraceId);
        Assert.Equal("req-3", pair.Value.Correlation.RequestId);
        Assert.Equal("op-7", pair.Value.Correlation.CorrelationId);
    }

    [Fact]
    public void A_snapshot_carries_the_correlation_of_its_most_recent_measurement()
    {
        var harness = Harness.Create(Now);
        harness.Engine.Count(
            Tenant, WorkflowMetricCollection.InstancesFailed, Key("k"), MetricCorrelation.For("first"));
        harness.Clock.Advance(TimeSpan.FromSeconds(30));
        harness.Engine.Count(
            Tenant, WorkflowMetricCollection.InstancesFailed, Key("k"), MetricCorrelation.For("latest"));

        var snapshot = harness.Engine.Snapshot(
            MetricInstance.Of(Tenant, WorkflowMetricCollection.InstancesFailed, Key("k")),
            MetricAggregation.Sum,
            TimeSpan.FromHours(1));

        Assert.Equal("latest", snapshot.Correlation.CorrelationId);
    }

    // ---- Permissions -------------------------------------------------------------------------------------------

    [Fact]
    public void Monitoring_rights_accumulate_across_the_identities_a_principal_presents()
    {
        var harness = Harness.Create(Now);
        harness.Permissions.Grant("u-ada", MonitoringPermission.ViewMetrics);
        harness.Permissions.Grant("role:platform-admin", MonitoringPermission.ManageThresholds);

        Assert.True(harness.Engine.Allows(MonitoringPermission.ViewMetrics, "u-ada"));
        Assert.False(harness.Engine.Allows(MonitoringPermission.ManageThresholds, "u-ada"));
        Assert.True(harness.Engine.Allows(
            MonitoringPermission.ManageThresholds, "u-ada", "role:platform-admin"));
    }

    [Fact]
    public void Nothing_is_readable_by_default()
    {
        var harness = Harness.Create(Now);

        // Metrics say how much work a factory did and where it went wrong; none of it is public by omission.
        Assert.False(harness.Engine.Allows(MonitoringPermission.ViewMetrics, "u-nobody"));
        Assert.False(harness.Engine.Allows(MonitoringPermission.ViewHealth, "u-nobody"));
    }

    // ---- The engine's own counters -----------------------------------------------------------------------------

    [Fact]
    public void The_engine_reports_on_itself()
    {
        var harness = Harness.Create(Now);
        harness.Repository.Register(new MetricDefinition(
            "test.sampled", MetricCategory.Performance, MetricKind.Counter, "count", "Sampled.")
        {
            SampleRate = 0.5,
        });
        harness.Engine.RegisterThreshold(new MetricThreshold(
            "sampled", "test.sampled", MetricComparison.GreaterThan, 0));

        harness.Engine.Count(Tenant, "test.sampled");
        harness.Engine.Count(Tenant, "test.sampled");
        harness.Engine.Evaluate(Tenant);

        var snapshot = harness.Engine.Snapshot();

        // An observability layer that cannot be observed is a blind spot in the worst possible place.
        Assert.Equal(1, snapshot.Collected);
        Assert.Equal(1, snapshot.Sampled);
        Assert.Equal(1, snapshot.ThresholdBreaches);
    }

    [Fact]
    public void Collection_events_are_off_until_something_asks_for_them()
    {
        var quiet = Harness.Create(Now);
        quiet.Engine.Count(Tenant, WorkflowMetricCollection.InstancesStarted, Key("k"));
        Assert.Empty(quiet.Events.Events.OfType<MetricCollected>());

        var loud = Harness.Create(Now, new MonitoringEngineOptions { PublishCollectionEvents = true });
        loud.Engine.Count(Tenant, WorkflowMetricCollection.InstancesStarted, Key("k"));
        Assert.Single(loud.Events.Events.OfType<MetricCollected>());
    }

    [Fact]
    public void The_catalogue_covers_all_thirteen_collections()
    {
        var categories = MetricCatalog.All.Select(definition => definition.Category).Distinct().ToArray();

        Assert.Equal(13, categories.Length);
        Assert.All(
            Enum.GetValues<MetricCategory>(),
            category => Assert.NotEmpty(MetricCatalog.For(category)));
    }

    private static MetricDimension Key(string value) =>
        MetricDimension.Of(MetricLabel.Of(MonitoringConstants.KeyLabel, value));

    private static void RegisterFailureAlert(Harness harness, TimeSpan waitFor)
    {
        harness.Engine.RegisterThreshold(new MetricThreshold(
            "failures", WorkflowMetricCollection.InstancesFailed, MetricComparison.GreaterThan, 0)
        {
            Window = TimeSpan.FromMinutes(5),
        });
        harness.Engine.RegisterAlertRule(new MetricAlertRule("workflow-failing", "failures")
        {
            For = waitFor,
        });
    }

    private sealed record Harness(
        MonitoringEngine Engine,
        IMetricRepository Repository,
        IMetricStore Store,
        IMonitoringPermissionStore Permissions,
        InMemoryMonitoringEventSink Events,
        MutableClock Clock)
    {
        internal static Harness Create(DateTimeOffset now, MonitoringEngineOptions? options = null)
        {
            var clock = new MutableClock(now);
            var engineOptions = options ?? new MonitoringEngineOptions();
            var repository = new InMemoryMetricRepository();
            var store = new InMemoryMetricStore();
            var healthRepository = new InMemoryHealthRepository();
            var healthStore = new InMemoryHealthStore();
            var permissions = new InMemoryMonitoringPermissionStore();
            var events = new InMemoryMonitoringEventSink();
            var dispatcher = new MonitoringDispatcher([events]);
            var diagnostics = new MonitoringMetrics();
            var aggregator = new MetricAggregator();
            var registry = new HealthRegistry(healthRepository);

            var runtime = new MonitoringRuntime(
                new MetricCollector(repository, store, new MetricSampler(), clock),
                aggregator,
                new MetricRetentionManager(repository, store, aggregator, engineOptions),
                new ThresholdEvaluator(repository, store, aggregator, engineOptions),
                new AlertEvaluator(repository),
                new MetricSearchService(store, repository, aggregator, engineOptions),
                new HealthEngine(
                    registry,
                    new HealthCheckExecutor(engineOptions),
                    healthStore,
                    store,
                    repository,
                    aggregator,
                    dispatcher,
                    engineOptions,
                    clock),
                dispatcher,
                diagnostics,
                engineOptions,
                clock);

            var engine = new MonitoringEngine(
                runtime, repository, registry, new MonitoringPermissionEvaluator(permissions), diagnostics);

            MetricCatalog.RegisterAll(repository);
            foreach (var (check, probe) in PlatformHealthChecks.All())
            {
                engine.RegisterHealthCheck(check, probe);
            }

            return new Harness(engine, repository, store, permissions, events, clock);
        }
    }
}
