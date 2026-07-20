using FactoryOS.Plugins.Reporting;
using FactoryOS.Plugins.Reporting.Domain;

namespace FactoryOS.Tests.Reporting;

public sealed class OeeCsvRendererTests
{
    private static InMemoryOeeReport PopulatedReport()
    {
        var report = new InMemoryOeeReport(new ReportingOptions());
        report.Record("acme", "press-1", new DateOnly(2026, 7, 19), 0.80m);
        report.Record("acme", "press-1", new DateOnly(2026, 7, 20), 0.90m);
        report.Record("acme", "oven-2", new DateOnly(2026, 7, 20), 0.75m);
        return report;
    }

    [Fact]
    public void Render_writes_a_header_and_one_row_per_machine_day_newest_first()
    {
        var (csv, rows) = OeeCsvRenderer.Render(PopulatedReport(), "acme");

        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(OeeCsvRenderer.Header, lines[0]);
        Assert.Equal(3, rows);
        Assert.Equal(4, lines.Length); // header + 3 rows

        // Machines ordered by id (oven-2 before press-1); each machine newest day first.
        Assert.StartsWith("oven-2,2026-07-20,", lines[1], StringComparison.Ordinal);
        Assert.StartsWith("press-1,2026-07-20,", lines[2], StringComparison.Ordinal);
        Assert.StartsWith("press-1,2026-07-19,", lines[3], StringComparison.Ordinal);
    }

    [Fact]
    public void Render_of_an_empty_tenant_is_header_only_with_zero_rows()
    {
        var (csv, rows) = OeeCsvRenderer.Render(new InMemoryOeeReport(new ReportingOptions()), "nobody");

        Assert.Equal(0, rows);
        Assert.Equal(OeeCsvRenderer.Header + "\n", csv);
    }

    [Fact]
    public void Render_is_deterministic()
    {
        var report = PopulatedReport();
        var first = OeeCsvRenderer.Render(report, "acme");
        var second = OeeCsvRenderer.Render(report, "acme");
        Assert.Equal(first.Csv, second.Csv);
    }
}
