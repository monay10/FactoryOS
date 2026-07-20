using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Maintenance;
using FactoryOS.Plugins.Maintenance.Domain;

namespace FactoryOS.Tests.Maintenance;

public sealed class RuleWorkOrderFactoryTests
{
    private static RuleTriggered Triggered(Guid eventId) => new()
    {
        EventId = eventId,
        Tenant = "acme",
        RuleId = "overtemp-press-1",
        Metric = "Temperature",
        MeterId = "press-1",
        Value = 90m,
        Operator = "GreaterThan",
        Threshold = 85m,
        Action = "RaiseMaintenanceAlert",
        TriggeredAt = DateTimeOffset.UnixEpoch,
        SourceEventId = Guid.NewGuid(),
    };

    [Fact]
    public void The_number_is_deterministic_per_trigger()
    {
        var id = Guid.NewGuid();
        var options = new MaintenanceOptions();

        var first = RuleWorkOrderFactory.FromTrigger(Triggered(id), options);
        var second = RuleWorkOrderFactory.FromTrigger(Triggered(id), options);

        Assert.Equal(first.Number, second.Number);
        Assert.StartsWith("WOR-", first.Number, StringComparison.Ordinal);
    }

    [Fact]
    public void The_order_targets_the_meter_and_is_due_per_options()
    {
        var options = new MaintenanceOptions { RuleWorkOrderDueInHours = 12 };
        var trigger = Triggered(Guid.NewGuid());

        var order = RuleWorkOrderFactory.FromTrigger(trigger, options);

        Assert.Equal("press-1", order.AssetCode);
        Assert.Equal("Open", order.Status);
        Assert.Equal(trigger.TriggeredAt.AddHours(12), order.DueAt);
    }
}
