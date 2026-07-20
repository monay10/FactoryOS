using FactoryOS.Connectors.Log;
using FactoryOS.Connectors.Log.Domain;
using FactoryOS.Contracts.Connectors;

namespace FactoryOS.Tests.ConnectorLog;

public sealed class LogTransportConnectorTests
{
    private static OutboundMessage Message(string tenant = "acme", string channel = "ops") => new()
    {
        Tenant = tenant,
        Channel = channel,
        Priority = "Critical",
        Subject = "Safety stand-down at site-1",
        Action = "Notify",
        OccurredAt = DateTimeOffset.UnixEpoch,
    };

    [Fact]
    public async Task Delivery_journals_the_message_and_reports_success()
    {
        var journal = new InMemoryDeliveryJournal();
        var connector = new LogTransportConnector(journal, new LogConnectorOptions());

        var result = await connector.DeliverAsync(Message(), CancellationToken.None);

        Assert.True(result.Delivered);
        var record = Assert.Single(journal.ForTenant("acme"));
        Assert.Equal("ops", record.Channel);
        Assert.Equal("Notify", record.Action);
    }

    [Fact]
    public void The_transport_name_comes_from_options()
    {
        var connector = new LogTransportConnector(new InMemoryDeliveryJournal(), new LogConnectorOptions { Transport = "audit" });

        Assert.Equal("audit", connector.Transport);
        Assert.Equal("log", connector.Key);
    }

    [Fact]
    public async Task The_journal_is_newest_first_and_tenant_isolated()
    {
        var journal = new InMemoryDeliveryJournal();
        var connector = new LogTransportConnector(journal, new LogConnectorOptions());

        await connector.DeliverAsync(Message(channel: "ops"), CancellationToken.None);
        await connector.DeliverAsync(Message(channel: "quality"), CancellationToken.None);

        var acme = journal.ForTenant("acme");
        Assert.Equal(2, acme.Count);
        Assert.Equal("quality", acme[0].Channel); // newest first
        Assert.Empty(journal.ForTenant("globex"));
    }
}
