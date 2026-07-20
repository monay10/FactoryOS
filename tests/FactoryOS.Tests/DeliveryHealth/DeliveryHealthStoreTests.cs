using FactoryOS.Plugins.DeliveryHealth;
using FactoryOS.Plugins.DeliveryHealth.Domain;

namespace FactoryOS.Tests.DeliveryHealth;

public sealed class DeliveryHealthStoreTests
{
    private static readonly DateTimeOffset At = new(2026, 7, 20, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Outcomes_are_tallied_per_transport()
    {
        var store = new InMemoryDeliveryHealthStore(new DeliveryHealthOptions());

        store.Record("acme", Guid.NewGuid(), "webhook", "ops", "s1", delivered: true, detail: null, At);
        store.Record("acme", Guid.NewGuid(), "webhook", "ops", "s2", delivered: false, detail: "503", At);
        store.Record("acme", Guid.NewGuid(), "log", "audit", "s3", delivered: true, detail: null, At);

        var health = store.ForTenant("acme");
        Assert.Equal(2, health.Count);

        // Ordered by transport ordinal: "log" before "webhook".
        Assert.Equal(new TransportHealth("log", 1, 1, 0), health[0]);
        Assert.Equal(new TransportHealth("webhook", 2, 1, 1), health[1]);
    }

    [Fact]
    public void Failures_are_retained_newest_first_and_bounded()
    {
        var store = new InMemoryDeliveryHealthStore(new DeliveryHealthOptions { RecentFailureCapacity = 2 });

        store.Record("acme", Guid.NewGuid(), "webhook", "ops", "first", delivered: false, detail: "500", At);
        store.Record("acme", Guid.NewGuid(), "webhook", "ops", "second", delivered: false, detail: "502", At);
        store.Record("acme", Guid.NewGuid(), "webhook", "ops", "third", delivered: false, detail: "503", At);

        var failures = store.RecentFailures("acme", 10);
        Assert.Equal(2, failures.Count); // bounded
        Assert.Equal("third", failures[0].Subject); // newest first
        Assert.Equal("second", failures[1].Subject);
        Assert.Equal("503", failures[0].Detail);
    }

    [Fact]
    public void Recording_is_idempotent_by_source_event_id()
    {
        var store = new InMemoryDeliveryHealthStore(new DeliveryHealthOptions());
        var id = Guid.NewGuid();

        Assert.True(store.Record("acme", id, "webhook", "ops", "s", delivered: false, detail: "x", At).Recorded);
        Assert.False(store.Record("acme", id, "webhook", "ops", "s", delivered: false, detail: "x", At).Recorded);

        var health = Assert.Single(store.ForTenant("acme"));
        Assert.Equal(1, health.Attempts);
        Assert.Single(store.RecentFailures("acme", 10));
    }

    [Fact]
    public void Consecutive_failures_streak_and_a_success_resets_it()
    {
        var store = new InMemoryDeliveryHealthStore(new DeliveryHealthOptions());

        Assert.Equal(1, store.Record("acme", Guid.NewGuid(), "webhook", "ops", "a", delivered: false, detail: null, At).ConsecutiveFailures);
        Assert.Equal(2, store.Record("acme", Guid.NewGuid(), "webhook", "ops", "b", delivered: false, detail: null, At).ConsecutiveFailures);
        Assert.Equal(0, store.Record("acme", Guid.NewGuid(), "webhook", "ops", "c", delivered: true, detail: null, At).ConsecutiveFailures);
        Assert.Equal(1, store.Record("acme", Guid.NewGuid(), "webhook", "ops", "d", delivered: false, detail: null, At).ConsecutiveFailures);
    }

    [Fact]
    public void Tenants_are_isolated()
    {
        var store = new InMemoryDeliveryHealthStore(new DeliveryHealthOptions());
        store.Record("acme", Guid.NewGuid(), "log", "audit", "s", delivered: true, detail: null, At);

        Assert.Empty(store.ForTenant("globex"));
        Assert.Single(store.ForTenant("acme"));
    }
}
