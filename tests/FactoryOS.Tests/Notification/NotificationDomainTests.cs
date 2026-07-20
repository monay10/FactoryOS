using FactoryOS.Plugins.Notification;
using FactoryOS.Plugins.Notification.Domain;

namespace FactoryOS.Tests.Notification;

public sealed class NotificationDomainTests
{
    [Fact]
    public void A_mapped_channel_resolves_to_its_transport()
    {
        var options = new NotificationOptions
        {
            ChannelTransports = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["ops"] = "sms" },
        };

        Assert.Equal("sms", TransportResolver.Resolve("ops", options));
    }

    [Fact]
    public void An_unmapped_channel_falls_back_to_the_default_transport()
    {
        var options = new NotificationOptions { DefaultTransport = "log" };

        Assert.Equal("log", TransportResolver.Resolve("unknown", options));
    }

    [Fact]
    public void Channel_lookup_is_case_insensitive()
    {
        var options = new NotificationOptions
        {
            ChannelTransports = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Ops"] = "sms" },
        };

        Assert.Equal("sms", TransportResolver.Resolve("ops", options));
    }

    private static NotificationRecord Record(string channel) =>
        new(channel, "log", "Normal", "subject", "Notify", DateTimeOffset.UnixEpoch);

    [Fact]
    public void The_outbox_records_newest_first_and_isolates_tenants()
    {
        var outbox = new InMemoryNotificationOutbox();
        outbox.TryRecord("acme", Guid.NewGuid(), Record("ops"));
        outbox.TryRecord("acme", Guid.NewGuid(), Record("quality"));

        var acme = outbox.ForTenant("acme");
        Assert.Equal(2, acme.Count);
        Assert.Equal("quality", acme[0].Channel); // newest first
        Assert.Empty(outbox.ForTenant("globex"));
    }

    [Fact]
    public void Recording_the_same_source_event_twice_is_a_no_op()
    {
        var outbox = new InMemoryNotificationOutbox();
        var id = Guid.NewGuid();

        Assert.True(outbox.TryRecord("acme", id, Record("ops")));
        Assert.False(outbox.TryRecord("acme", id, Record("ops")));
        Assert.Single(outbox.ForTenant("acme"));
    }
}
