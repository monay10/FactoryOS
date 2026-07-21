using FactoryOS.Plugins.Workflow.Notifications.Channels;
using FactoryOS.Plugins.Workflow.Notifications.Configuration;
using FactoryOS.Plugins.Workflow.Notifications.Diagnostics;
using FactoryOS.Plugins.Workflow.Notifications.Domain;
using FactoryOS.Plugins.Workflow.Notifications.Events;
using FactoryOS.Plugins.Workflow.Notifications.Execution;
using FactoryOS.Plugins.Workflow.Notifications.Persistence;
using FactoryOS.Tests.Identity;

namespace FactoryOS.Tests.Workflow.Notifications;

/// <summary>
/// Unit coverage of the notification engine core: recipient resolution (user / role / group / dynamic), the
/// template engine, routing, preferences and subscriptions, the delivery queue, retries with back-off, the
/// dead-letter queue, delivery over channels, and history — exercised directly, without a container and without
/// the source engines. Event-driven integration is proven in the integration suite.
/// </summary>
public sealed class NotificationEngineCoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 07, 21, 09, 00, 00, TimeSpan.Zero);

    // ---- Template engine ------------------------------------------------------------------------------------

    [Fact]
    public void The_template_engine_substitutes_known_tokens_and_blanks_unknown_ones()
    {
        var engine = new NotificationTemplateEngine();
        var template = new NotificationTemplate("t", NotificationChannel.Email, "Hi {{name}}, total {{amount}}.", "Order {{id}}");
        var values = new Dictionary<string, object?> { ["name"] = "Alice", ["id"] = 7 };

        var rendered = engine.Render(template, values);

        Assert.Equal("Order 7", rendered.Subject);
        Assert.Equal("Hi Alice, total .", rendered.Body);
    }

    // ---- Recipient resolver ---------------------------------------------------------------------------------

    [Fact]
    public void The_recipient_resolver_expands_a_role_to_its_members()
    {
        var directory = new InMemoryNotificationDirectory();
        directory.AddUser(new NotificationRecipient("u1"));
        directory.AddUser(new NotificationRecipient("u2"));
        directory.AddToRole("ops", "u1");
        directory.AddToRole("ops", "u2");
        var resolver = new RecipientResolver(directory);

        var recipients = resolver.Resolve([NotificationAssignment.ToRole("ops")], Empty);

        Assert.Equal(
            "u1,u2",
            string.Join(',', recipients.Select(recipient => recipient.UserId).OrderBy(id => id, StringComparer.Ordinal)));
    }

    [Fact]
    public void The_recipient_resolver_resolves_a_dynamic_assignment_from_context_values()
    {
        var directory = new InMemoryNotificationDirectory();
        directory.AddUser(new NotificationRecipient("owner-42"));
        var resolver = new RecipientResolver(directory);
        var values = new Dictionary<string, object?> { ["owner"] = "owner-42" };

        var recipients = resolver.Resolve([NotificationAssignment.Dynamic("owner")], values);

        Assert.Equal("owner-42", Assert.Single(recipients).UserId);
    }

    [Fact]
    public void An_unknown_user_still_resolves_to_an_addressless_recipient()
    {
        var resolver = new RecipientResolver(new InMemoryNotificationDirectory());

        var recipient = Assert.Single(resolver.Resolve([NotificationAssignment.ToUser("ghost")], Empty));

        Assert.Equal("ghost", recipient.UserId);
        Assert.Null(recipient.AddressFor(NotificationChannel.Email));
    }

    // ---- Routing / delivery ---------------------------------------------------------------------------------

    [Fact]
    public async Task Routing_produces_one_notification_per_recipient_and_channel_and_delivers_it()
    {
        var harness = Harness.Create(Now);
        harness.Directory.AddUser(new NotificationRecipient("u-alice")
            .WithAddress(NotificationChannel.Email, "alice@example.com"));

        var produced = await harness.Engine.NotifyAsync(
            new NotificationRequest
            {
                Category = NotificationCategory.General,
                Channels = [NotificationChannel.Email, NotificationChannel.InApp],
                Recipients = [NotificationAssignment.ToUser("u-alice")],
                Subject = "Hello",
                Body = "Body",
            },
            new NotificationContext("acme"));

        Assert.Equal(2, produced.Count);
        Assert.All(produced, notification => Assert.Equal(NotificationStatus.Delivered, notification.Status));
        Assert.Single(harness.Outbox.ForChannel(NotificationChannel.Email));
        Assert.Single(harness.Outbox.ForChannel(NotificationChannel.InApp));
    }

    [Fact]
    public async Task A_rule_can_raise_priority_from_the_payload()
    {
        var harness = Harness.Create(Now);
        harness.Directory.AddUser(new NotificationRecipient("u-alice"));
        harness.Engine.Register(NotificationDefinition.Create("big", "Big")
            .InCategory(NotificationCategory.Alert)
            .OnChannel(NotificationChannel.InApp)
            .ToRecipient(NotificationAssignment.ToUser("u-alice"))
            .AddRule(new NotificationRule("amount > 1000", priority: NotificationPriority.Critical))
            .Build());

        var produced = await harness.Engine.NotifyAsync(
            new NotificationRequest { Category = NotificationCategory.Alert, DefinitionKey = "big", Body = "big spend" },
            new NotificationContext("acme", values: new Dictionary<string, object?> { ["amount"] = 5000 }));

        Assert.Equal(NotificationPriority.Critical, Assert.Single(produced).Priority);
    }

    // ---- Preferences ----------------------------------------------------------------------------------------

    [Fact]
    public async Task A_muted_category_suppresses_the_notification()
    {
        var harness = Harness.Create(Now);
        harness.Directory.AddUser(new NotificationRecipient("u-alice"));
        harness.Engine.SetPreference(new NotificationPreference("u-alice",
            mutedCategories: [NotificationCategory.Digest]));

        var produced = await harness.Engine.NotifyAsync(
            new NotificationRequest
            {
                Category = NotificationCategory.Digest,
                Channels = [NotificationChannel.InApp],
                Recipients = [NotificationAssignment.ToUser("u-alice")],
                Body = "digest",
            },
            new NotificationContext("acme"));

        Assert.Empty(produced);
        Assert.Contains(harness.Events.Events, e => e is NotificationSuppressed);
    }

    [Fact]
    public async Task A_channel_allow_list_narrows_the_channels()
    {
        var harness = Harness.Create(Now);
        harness.Directory.AddUser(new NotificationRecipient("u-alice")
            .WithAddress(NotificationChannel.Email, "alice@example.com"));
        harness.Engine.SetPreference(new NotificationPreference("u-alice",
            allowedChannels: [NotificationChannel.InApp]));

        var produced = await harness.Engine.NotifyAsync(
            new NotificationRequest
            {
                Category = NotificationCategory.General,
                Channels = [NotificationChannel.Email, NotificationChannel.InApp],
                Recipients = [NotificationAssignment.ToUser("u-alice")],
                Body = "hi",
            },
            new NotificationContext("acme"));

        Assert.Equal(NotificationChannel.InApp, Assert.Single(produced).Channel);
    }

    [Fact]
    public async Task A_critical_notification_bypasses_a_muted_category()
    {
        var harness = Harness.Create(Now);
        harness.Directory.AddUser(new NotificationRecipient("u-alice"));
        harness.Engine.SetPreference(new NotificationPreference("u-alice",
            mutedCategories: [NotificationCategory.Alert]));

        var produced = await harness.Engine.NotifyAsync(
            new NotificationRequest
            {
                Category = NotificationCategory.Alert,
                Channels = [NotificationChannel.InApp],
                Priority = NotificationPriority.Critical,
                Recipients = [NotificationAssignment.ToUser("u-alice")],
                Body = "critical",
            },
            new NotificationContext("acme"));

        Assert.Single(produced);
    }

    // ---- Subscriptions --------------------------------------------------------------------------------------

    [Fact]
    public async Task A_subscriber_receives_a_category_notification()
    {
        var harness = Harness.Create(Now);
        harness.Directory.AddUser(new NotificationRecipient("u-ops"));
        harness.Engine.Subscribe(new NotificationSubscription("u-ops", NotificationCategory.Workflow));

        var produced = await harness.Engine.NotifyAsync(
            new NotificationRequest { Category = NotificationCategory.Workflow, Source = "workflow", Body = "done" },
            new NotificationContext("acme"));

        Assert.Equal("u-ops", Assert.Single(produced).RecipientUserId);
    }

    [Fact]
    public async Task A_source_scoped_subscription_only_matches_its_source()
    {
        var harness = Harness.Create(Now);
        harness.Directory.AddUser(new NotificationRecipient("u-ops"));
        harness.Engine.Subscribe(new NotificationSubscription("u-ops", NotificationCategory.Workflow, sourceKey: "wf-a"));

        var other = await harness.Engine.NotifyAsync(
            new NotificationRequest { Category = NotificationCategory.Workflow, SourceKey = "wf-b", Body = "b" },
            new NotificationContext("acme"));
        var matched = await harness.Engine.NotifyAsync(
            new NotificationRequest { Category = NotificationCategory.Workflow, SourceKey = "wf-a", Body = "a" },
            new NotificationContext("acme"));

        Assert.Empty(other);
        Assert.Single(matched);
    }

    // ---- Queue ----------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_scheduled_notification_waits_in_the_queue_until_it_is_due()
    {
        var harness = Harness.Create(Now);
        harness.Directory.AddUser(new NotificationRecipient("u-alice"));

        var produced = harness.Engine.Notify(
            new NotificationRequest
            {
                Category = NotificationCategory.General,
                Channels = [NotificationChannel.InApp],
                Recipients = [NotificationAssignment.ToUser("u-alice")],
                DeliveryPolicy = NotificationDeliveryPolicy.Scheduled,
                ScheduledForUtc = Now.AddMinutes(30),
                Body = "later",
            },
            new NotificationContext("acme"));
        var notification = Assert.Single(produced);

        var early = await harness.Engine.ProcessDueAsync();
        Assert.Equal(0, early.Delivered);
        Assert.Equal(NotificationStatus.Queued, harness.Engine.GetNotification(notification.Id)!.Status);

        harness.Clock.Advance(TimeSpan.FromHours(1));
        var late = await harness.Engine.ProcessDueAsync();
        Assert.Equal(1, late.Delivered);
        Assert.Equal(NotificationStatus.Delivered, harness.Engine.GetNotification(notification.Id)!.Status);
    }

    // ---- Retry / dead letter --------------------------------------------------------------------------------

    [Fact]
    public async Task A_transient_failure_is_retried_and_then_delivered()
    {
        var flaky = new FlakyChannelSender(NotificationChannel.InApp, failFirst: 1);
        var harness = Harness.Create(Now, flaky);
        harness.Directory.AddUser(new NotificationRecipient("u-alice"));

        var produced = await harness.Engine.NotifyAsync(
            RequestFor("u-alice", new NotificationRetryPolicy(3, TimeSpan.FromMinutes(1))),
            new NotificationContext("acme"));
        var notification = Assert.Single(produced);

        Assert.Equal(NotificationStatus.Retrying, harness.Engine.GetNotification(notification.Id)!.Status);
        Assert.Contains(harness.Events.Events, e => e is NotificationRetried);

        harness.Clock.Advance(TimeSpan.FromMinutes(2));
        var pass = await harness.Engine.ProcessDueAsync();

        Assert.Equal(1, pass.Delivered);
        var delivered = harness.Engine.GetNotification(notification.Id)!;
        Assert.Equal(NotificationStatus.Delivered, delivered.Status);
        Assert.Equal(2, delivered.Attempts);
    }

    [Fact]
    public async Task A_notification_that_keeps_failing_is_dead_lettered()
    {
        var failing = new FlakyChannelSender(NotificationChannel.InApp, failFirst: int.MaxValue);
        var harness = Harness.Create(Now, failing);
        harness.Directory.AddUser(new NotificationRecipient("u-alice"));

        var produced = await harness.Engine.NotifyAsync(
            RequestFor("u-alice", new NotificationRetryPolicy(2, TimeSpan.Zero)),
            new NotificationContext("acme"));
        var notification = Assert.Single(produced);

        // First attempt failed inside NotifyAsync; run the retry pass to exhaust the budget.
        await harness.Engine.ProcessDueAsync();

        var dead = harness.Engine.GetNotification(notification.Id)!;
        Assert.Equal(NotificationStatus.DeadLettered, dead.Status);
        Assert.Contains(harness.Engine.DeadLetters(), n => n.Id == notification.Id);
        Assert.Contains(harness.Events.Events, e => e is NotificationFailed { DeadLettered: true });
    }

    [Fact]
    public async Task A_dead_lettered_notification_can_be_requeued()
    {
        var failing = new FlakyChannelSender(NotificationChannel.InApp, failFirst: 1);
        var harness = Harness.Create(Now, failing);
        harness.Directory.AddUser(new NotificationRecipient("u-alice"));

        var produced = await harness.Engine.NotifyAsync(
            RequestFor("u-alice", new NotificationRetryPolicy(1, TimeSpan.Zero)),
            new NotificationContext("acme"));
        var notification = Assert.Single(produced);
        Assert.Equal(NotificationStatus.DeadLettered, harness.Engine.GetNotification(notification.Id)!.Status);

        // The next attempt will succeed (only the first was set to fail); requeue and process.
        Assert.NotNull(harness.Engine.RequeueDeadLetter(notification.Id));
        var pass = await harness.Engine.ProcessDueAsync();

        Assert.Equal(1, pass.Delivered);
        Assert.Equal(NotificationStatus.Delivered, harness.Engine.GetNotification(notification.Id)!.Status);
    }

    [Fact]
    public async Task A_missing_channel_address_fails_delivery()
    {
        var harness = Harness.Create(Now);
        harness.Directory.AddUser(new NotificationRecipient("u-alice")); // no e-mail address

        var produced = await harness.Engine.NotifyAsync(
            new NotificationRequest
            {
                Category = NotificationCategory.General,
                Channels = [NotificationChannel.Email],
                Recipients = [NotificationAssignment.ToUser("u-alice")],
                Retry = new NotificationRetryPolicy(1, TimeSpan.Zero),
                Body = "hi",
            },
            new NotificationContext("acme"));
        var notification = Assert.Single(produced);

        Assert.Equal(NotificationStatus.DeadLettered, harness.Engine.GetNotification(notification.Id)!.Status);
        Assert.Contains(harness.Events.Events,
            e => e is NotificationFailed { Reason: NotificationFailureReason.MissingAddress });
    }

    // ---- History / read / cancel ----------------------------------------------------------------------------

    [Fact]
    public async Task Delivery_history_records_the_lifecycle()
    {
        var harness = Harness.Create(Now);
        harness.Directory.AddUser(new NotificationRecipient("u-alice"));

        var produced = await harness.Engine.NotifyAsync(
            new NotificationRequest
            {
                Category = NotificationCategory.General,
                Channels = [NotificationChannel.InApp],
                Recipients = [NotificationAssignment.ToUser("u-alice")],
                Body = "hi",
            },
            new NotificationContext("acme"));
        var actions = harness.Engine.GetHistory(Assert.Single(produced).Id).Select(entry => entry.Action).ToArray();

        Assert.Contains(NotificationHistoryAction.Queued, actions);
        Assert.Contains(NotificationHistoryAction.Sending, actions);
        Assert.Contains(NotificationHistoryAction.Delivered, actions);
    }

    [Fact]
    public async Task A_delivered_notification_can_be_marked_read()
    {
        var harness = Harness.Create(Now);
        harness.Directory.AddUser(new NotificationRecipient("u-alice"));
        var produced = await harness.Engine.NotifyAsync(
            new NotificationRequest
            {
                Category = NotificationCategory.General,
                Channels = [NotificationChannel.InApp],
                Recipients = [NotificationAssignment.ToUser("u-alice")],
                Body = "hi",
            },
            new NotificationContext("acme"));

        var read = harness.Engine.MarkRead(Assert.Single(produced).Id);

        Assert.Equal(NotificationStatus.Read, read!.Status);
        Assert.Contains(harness.Events.Events, e => e is NotificationRead);
    }

    [Fact]
    public void A_queued_notification_can_be_cancelled_before_delivery()
    {
        var harness = Harness.Create(Now);
        harness.Directory.AddUser(new NotificationRecipient("u-alice"));
        var produced = harness.Engine.Notify(
            new NotificationRequest
            {
                Category = NotificationCategory.General,
                Channels = [NotificationChannel.InApp],
                Recipients = [NotificationAssignment.ToUser("u-alice")],
                DeliveryPolicy = NotificationDeliveryPolicy.Scheduled,
                ScheduledForUtc = Now.AddHours(1),
                Body = "later",
            },
            new NotificationContext("acme"));

        var cancelled = harness.Engine.Cancel(Assert.Single(produced).Id, "admin");

        Assert.Equal(NotificationStatus.Cancelled, cancelled!.Status);
    }

    // ---- Digest ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task Digest_notifications_are_folded_into_one_delivery()
    {
        var harness = Harness.Create(Now);
        harness.Directory.AddUser(new NotificationRecipient("u-alice"));

        for (var i = 0; i < 3; i++)
        {
            harness.Engine.Notify(
                new NotificationRequest
                {
                    Category = NotificationCategory.General,
                    Channels = [NotificationChannel.InApp],
                    Recipients = [NotificationAssignment.ToUser("u-alice")],
                    DeliveryPolicy = NotificationDeliveryPolicy.Digest,
                    Body = $"item {i}",
                },
                new NotificationContext("acme"));
        }

        var early = await harness.Engine.ProcessDueAsync();
        Assert.Equal(0, early.Delivered); // held for the digest, not delivered individually

        var digests = await harness.Engine.FlushDigestsAsync();

        Assert.Equal(1, digests);
        Assert.Single(harness.Outbox.ForChannel(NotificationChannel.InApp));
    }

    // ---- Helpers ------------------------------------------------------------------------------------------

    private static readonly IReadOnlyDictionary<string, object?> Empty =
        new Dictionary<string, object?>(StringComparer.Ordinal);

    private static NotificationRequest RequestFor(string userId, NotificationRetryPolicy retry) => new()
    {
        Category = NotificationCategory.General,
        Channels = [NotificationChannel.InApp],
        Recipients = [NotificationAssignment.ToUser(userId)],
        Retry = retry,
        Body = "hi",
    };

    /// <summary>A fully in-memory notification pipeline wired by hand for unit tests.</summary>
    private sealed class Harness
    {
        private Harness(
            NotificationEngine engine,
            InMemoryNotificationDirectory directory,
            InMemoryNotificationOutbox outbox,
            InMemoryNotificationEventSink events,
            MutableClock clock)
        {
            Engine = engine;
            Directory = directory;
            Outbox = outbox;
            Events = events;
            Clock = clock;
        }

        public NotificationEngine Engine { get; }

        public InMemoryNotificationDirectory Directory { get; }

        public InMemoryNotificationOutbox Outbox { get; }

        public InMemoryNotificationEventSink Events { get; }

        public MutableClock Clock { get; }

        public static Harness Create(DateTimeOffset now, params INotificationChannelSender[] senders)
        {
            var clock = new MutableClock(now);
            var outbox = new InMemoryNotificationOutbox();
            var store = new InMemoryNotificationStore();
            var history = new InMemoryNotificationHistoryRepository();
            var events = new InMemoryNotificationEventSink();
            var metrics = new NotificationMetrics();
            var definitions = new InMemoryNotificationRepository();
            var templates = new InMemoryNotificationTemplateRepository();
            var directory = new InMemoryNotificationDirectory();
            var preferences = new InMemoryNotificationPreferenceStore();
            var subscriptions = new InMemoryNotificationSubscriptionStore();
            var options = new NotificationEngineOptions();

            IEnumerable<INotificationChannelSender> channelSenders = senders.Length > 0
                ? senders
                : DefaultSenders(outbox);

            var templateEngine = new NotificationTemplateEngine();
            var recipientResolver = new RecipientResolver(directory);
            var preferenceResolver = new PreferenceResolver(preferences);
            var router = new NotificationRouter(
                recipientResolver, preferenceResolver, subscriptions, templates, templateEngine, options);
            var dispatcher = new NotificationDispatcher(channelSenders, store, history, events, metrics, clock);
            var queue = new NotificationQueue(store, history, events, metrics, clock);
            var retry = new NotificationRetryService(store, history, events, metrics, clock);
            var processor = new NotificationQueueProcessor(queue, dispatcher, retry, store, history, events, options, clock);
            var deadLetters = new DeadLetterQueue(store, history, clock);
            var runtime = new NotificationRuntime(
                router, queue, processor, definitions, store, history, events, metrics, clock);
            var engine = new NotificationEngine(runtime, deadLetters, templates, preferences, subscriptions, metrics);

            return new Harness(engine, directory, outbox, events, clock);
        }

        private static INotificationChannelSender[] DefaultSenders(InMemoryNotificationOutbox outbox) =>
        [
            new EmailChannelSender(outbox),
            new SmsChannelSender(outbox),
            new PushChannelSender(outbox),
            new TeamsChannelSender(outbox),
            new SlackChannelSender(outbox),
            new WebhookChannelSender(outbox),
            new InAppChannelSender(outbox),
            new SignalRChannelSender(outbox),
        ];
    }

    /// <summary>A test channel sender that fails a set number of initial attempts, then succeeds.</summary>
    private sealed class FlakyChannelSender : INotificationChannelSender
    {
        private readonly int _failFirst;
        private int _attempts;

        public FlakyChannelSender(NotificationChannel channel, int failFirst)
        {
            Channel = channel;
            _failFirst = failFirst;
        }

        public NotificationChannel Channel { get; }

        public Task<ChannelSendResult> SendAsync(
            OutboundNotification message, CancellationToken cancellationToken = default)
        {
            _attempts++;
            return Task.FromResult(_attempts <= _failFirst
                ? ChannelSendResult.Fail(NotificationFailureReason.TransportError, "transient")
                : ChannelSendResult.Ok($"ok-{_attempts}"));
        }
    }
}
