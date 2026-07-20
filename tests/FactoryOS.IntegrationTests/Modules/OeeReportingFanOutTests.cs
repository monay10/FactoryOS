using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Dashboard;
using FactoryOS.Plugins.Dashboard.Domain;
using FactoryOS.Plugins.Reporting;
using FactoryOS.Plugins.Reporting.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// Fan-out over the real bus: a single OEE fact feeds two independent Experience read-models at once — the
/// Dashboard's live tile and the Reporting daily rollup — neither aware of the other. Publishing two readings
/// for one machine on one day yields the latest tile on the board and a two-sample daily average in the report.
/// </summary>
public sealed class OeeReportingFanOutTests
{
    [Fact]
    public async Task One_oee_fact_feeds_both_the_board_and_the_daily_report()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBus();
        new DashboardPlugin().ConfigureServices(services);
        new ReportingPlugin().ConfigureServices(services);

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();
        var board = provider.GetRequiredService<IOperationsBoard>();
        var report = provider.GetRequiredService<IOeeReport>();

        var periodEnd = new DateTimeOffset(2026, 7, 20, 6, 0, 0, TimeSpan.Zero);
        await bus.PublishAsync(new OeeCalculated
        {
            Tenant = "acme",
            MachineId = "press-1",
            Oee = 0.60m,
            MeetsTarget = false,
            PeriodEnd = periodEnd,
        });
        await bus.PublishAsync(new OeeCalculated
        {
            Tenant = "acme",
            MachineId = "press-1",
            Oee = 0.80m,
            MeetsTarget = true,
            PeriodEnd = periodEnd.AddHours(6),
        });

        // Dashboard: the latest tile wins.
        var tile = Assert.Single(board.Snapshot("acme").Machines);
        Assert.Equal(0.80m, tile.Oee);

        // Reporting: both readings fold into one day, averaged.
        var stat = Assert.Single(report.ForMachine("acme", "press-1"));
        Assert.Equal(new DateOnly(2026, 7, 20), stat.Day);
        Assert.Equal(2, stat.SampleCount);
        Assert.Equal(0.70m, stat.AverageOee);
        Assert.Equal(0.60m, stat.MinOee);
        Assert.Equal(0.80m, stat.MaxOee);
    }
}
