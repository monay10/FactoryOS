using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Reporting;
using FactoryOS.Plugins.Reporting.Application;
using FactoryOS.Plugins.Reporting.Domain;

namespace FactoryOS.Tests.Reporting;

public sealed class OeeCalculatedHandlerTests
{
    private sealed record Harness(OeeCalculatedHandler Handler, IOeeReport Report);

    private static Harness Build()
    {
        var report = new InMemoryOeeReport(new ReportingOptions());
        return new Harness(new OeeCalculatedHandler(report, new InMemoryProcessedEventLog()), report);
    }

    private static OeeCalculated Oee(Guid? id = null, decimal oee = 0.8m) => new()
    {
        EventId = id ?? Guid.NewGuid(),
        Tenant = "acme",
        MachineId = "m-1",
        Oee = oee,
        MeetsTarget = true,
        PeriodEnd = new DateTimeOffset(2026, 7, 20, 6, 0, 0, TimeSpan.Zero),
    };

    private static EventContext Context(IIntegrationEvent e) =>
        new(Guid.NewGuid(), e.EventId, Guid.NewGuid(), null, "trace", EventPriority.Normal, 1, e.OccurredOnUtc);

    [Fact]
    public async Task Folds_a_reading_into_its_utc_day_bucket()
    {
        var h = Build();
        var evt = Oee();

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        var stat = Assert.Single(h.Report.ForMachine("acme", "m-1"));
        Assert.Equal(new DateOnly(2026, 7, 20), stat.Day);
        Assert.Equal(1, stat.SampleCount);
        Assert.Equal(0.8m, stat.AverageOee);
    }

    [Fact]
    public async Task A_redelivered_reading_does_not_skew_the_average()
    {
        var h = Build();
        var evt = Oee(oee: 0.6m);

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);
        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None); // same event id

        var stat = Assert.Single(h.Report.ForMachine("acme", "m-1"));
        Assert.Equal(1, stat.SampleCount);
        Assert.Equal(0.6m, stat.AverageOee);
    }
}
