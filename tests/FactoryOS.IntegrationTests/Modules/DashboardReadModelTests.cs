using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Dashboard;
using FactoryOS.Plugins.Dashboard.Domain;
using FactoryOS.Plugins.Safety;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// The Experience-layer read model, proven over the real bus. A severe safety incident becomes — through the
/// Safety module — a stand-down the Dashboard folds into a critical alert, while an OEE fact from the OEE module
/// becomes a machine tile. The board assembles a cross-module operations picture with no module referencing the
/// Dashboard and the Dashboard referencing no module. `SafetyIncidentReported → SafetyStandDownTriggered → board`.
/// </summary>
public sealed class DashboardReadModelTests
{
    [Fact]
    public async Task The_board_assembles_a_live_picture_from_multiple_modules()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBus();
        new SafetyPlugin().ConfigureServices(services);
        new DashboardPlugin().ConfigureServices(services);

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();
        var board = provider.GetRequiredService<IOperationsBoard>();

        // OEE fact straight from the OEE module's shared vocabulary.
        await bus.PublishAsync(new OeeCalculated
        {
            Tenant = "acme",
            MachineId = "press-1",
            Oee = 0.76m,
            MeetsTarget = false,
            PeriodEnd = DateTimeOffset.UnixEpoch,
        });

        // A severe incident: Safety turns it into a stand-down, which the board folds into a critical alert.
        await bus.PublishAsync(new SafetyIncidentReported
        {
            Tenant = "acme",
            SiteId = "site-1",
            Severity = 5,
            Category = "Chemical",
            OccurredAt = DateTimeOffset.UnixEpoch,
        });

        var snapshot = board.Snapshot("acme");

        var tile = Assert.Single(snapshot.Machines);
        Assert.Equal("press-1", tile.MachineId);
        Assert.Equal(0.76m, tile.Oee);

        var alert = Assert.Single(snapshot.RecentAlerts);
        Assert.Equal(nameof(SafetyStandDownTriggered), alert.Kind);
        Assert.Equal(AlertLevels.Critical, alert.Level);
        Assert.Equal(1, snapshot.CriticalAlertCount);
    }
}
