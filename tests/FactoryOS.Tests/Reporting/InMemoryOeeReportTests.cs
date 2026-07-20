using FactoryOS.Plugins.Reporting;
using FactoryOS.Plugins.Reporting.Domain;

namespace FactoryOS.Tests.Reporting;

public sealed class InMemoryOeeReportTests
{
    private static InMemoryOeeReport Report(int retainDays = 90) =>
        new(new ReportingOptions { RetainDays = retainDays });

    private static DateOnly Day(int d) => new(2026, 7, d);

    [Fact]
    public void An_unknown_machine_has_no_stats()
    {
        Assert.Empty(Report().ForMachine("acme", "m-1"));
    }

    [Fact]
    public void Readings_on_the_same_day_average_and_track_min_max()
    {
        var report = Report();
        report.Record("acme", "m-1", Day(1), 0.60m);
        report.Record("acme", "m-1", Day(1), 0.80m);
        report.Record("acme", "m-1", Day(1), 0.70m);

        var stat = Assert.Single(report.ForMachine("acme", "m-1"));
        Assert.Equal(Day(1), stat.Day);
        Assert.Equal(3, stat.SampleCount);
        Assert.Equal(0.70m, stat.AverageOee);
        Assert.Equal(0.60m, stat.MinOee);
        Assert.Equal(0.80m, stat.MaxOee);
    }

    [Fact]
    public void Days_are_returned_newest_first()
    {
        var report = Report();
        report.Record("acme", "m-1", Day(1), 0.5m);
        report.Record("acme", "m-1", Day(3), 0.9m);
        report.Record("acme", "m-1", Day(2), 0.7m);

        var days = report.ForMachine("acme", "m-1");

        Assert.Equal(3, days.Count);
        Assert.Equal(Day(3), days[0].Day);
        Assert.Equal(Day(2), days[1].Day);
        Assert.Equal(Day(1), days[2].Day);
    }

    [Fact]
    public void Retention_trims_the_oldest_days()
    {
        var report = Report(retainDays: 2);
        report.Record("acme", "m-1", Day(1), 0.5m);
        report.Record("acme", "m-1", Day(2), 0.6m);
        report.Record("acme", "m-1", Day(3), 0.7m);

        var days = report.ForMachine("acme", "m-1");

        Assert.Equal(2, days.Count);
        Assert.Equal(Day(3), days[0].Day);
        Assert.Equal(Day(2), days[1].Day); // Day(1) trimmed
    }

    [Fact]
    public void Machines_and_tenants_are_isolated()
    {
        var report = Report();
        report.Record("acme", "m-1", Day(1), 0.5m);

        Assert.Empty(report.ForMachine("acme", "m-2"));
        Assert.Empty(report.ForMachine("globex", "m-1"));
    }
}
