using FactoryOS.Plugins.Dashboard;
using FactoryOS.Plugins.Dashboard.Domain;

namespace FactoryOS.Tests.Dashboard;

public sealed class InMemoryOperationsBoardTests
{
    private static InMemoryOperationsBoard Board(int capacity = 50) =>
        new(new DashboardOptions { RecentAlertCapacity = capacity });

    [Fact]
    public void An_unknown_tenant_has_an_empty_board()
    {
        var snapshot = Board().Snapshot("acme");

        Assert.Equal("acme", snapshot.Tenant);
        Assert.Empty(snapshot.Machines);
        Assert.Empty(snapshot.RecentAlerts);
        Assert.Equal(0, snapshot.CriticalAlertCount);
    }

    [Fact]
    public void The_latest_oee_per_machine_wins_and_machines_are_ordered()
    {
        var board = Board();
        board.RecordOee("acme", new OeeTile("m-2", 0.5m, false, DateTimeOffset.UnixEpoch));
        board.RecordOee("acme", new OeeTile("m-1", 0.7m, true, DateTimeOffset.UnixEpoch));
        board.RecordOee("acme", new OeeTile("m-1", 0.9m, true, DateTimeOffset.UnixEpoch.AddHours(1)));

        var machines = board.Snapshot("acme").Machines;

        Assert.Equal(2, machines.Count);
        Assert.Equal("m-1", machines[0].MachineId);
        Assert.Equal(0.9m, machines[0].Oee); // last write won
        Assert.Equal("m-2", machines[1].MachineId);
    }

    [Fact]
    public void Alerts_are_newest_first_and_critical_ones_are_counted()
    {
        var board = Board();
        board.PushAlert("acme", new AlertTile("QualityAlertRaised", AlertLevels.Warning, "q", DateTimeOffset.UnixEpoch));
        board.PushAlert("acme", new AlertTile("SafetyStandDownTriggered", AlertLevels.Critical, "s", DateTimeOffset.UnixEpoch.AddMinutes(1)));

        var snapshot = board.Snapshot("acme");

        Assert.Equal("SafetyStandDownTriggered", snapshot.RecentAlerts[0].Kind); // newest first
        Assert.Equal("QualityAlertRaised", snapshot.RecentAlerts[1].Kind);
        Assert.Equal(1, snapshot.CriticalAlertCount);
    }

    [Fact]
    public void The_alert_feed_is_bounded_to_capacity_dropping_the_oldest()
    {
        var board = Board(capacity: 2);
        board.PushAlert("acme", new AlertTile("A", AlertLevels.Warning, "1", DateTimeOffset.UnixEpoch));
        board.PushAlert("acme", new AlertTile("B", AlertLevels.Warning, "2", DateTimeOffset.UnixEpoch.AddMinutes(1)));
        board.PushAlert("acme", new AlertTile("C", AlertLevels.Warning, "3", DateTimeOffset.UnixEpoch.AddMinutes(2)));

        var alerts = board.Snapshot("acme").RecentAlerts;

        Assert.Equal(2, alerts.Count);
        Assert.Equal("C", alerts[0].Kind);
        Assert.Equal("B", alerts[1].Kind); // "A" fell off the tail
    }

    [Fact]
    public void Boards_are_isolated_per_tenant()
    {
        var board = Board();
        board.RecordOee("acme", new OeeTile("m-1", 0.8m, true, DateTimeOffset.UnixEpoch));
        board.PushAlert("acme", new AlertTile("SafetyStandDownTriggered", AlertLevels.Critical, "s", DateTimeOffset.UnixEpoch));

        var other = board.Snapshot("globex");

        Assert.Empty(other.Machines);
        Assert.Empty(other.RecentAlerts);
    }
}
