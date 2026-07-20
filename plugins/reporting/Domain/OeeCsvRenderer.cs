using System.Globalization;
using System.Text;

namespace FactoryOS.Plugins.Reporting.Domain;

/// <summary>
/// Renders the OEE read-model into a CSV artifact. Pure and deterministic: given the same report state it
/// produces byte-for-byte the same document, so a re-run stores an identical object. No tenant scan, no I/O —
/// the caller supplies the already-scoped rows.
/// </summary>
public static class OeeCsvRenderer
{
    /// <summary>The CSV header row (no trailing newline).</summary>
    public const string Header = "machine,day,samples,average_oee,min_oee,max_oee";

    /// <summary>Renders a tenant's OEE report to CSV.</summary>
    /// <param name="report">The read-model to render from.</param>
    /// <param name="tenant">The tenant whose machines to render, newest day first per machine.</param>
    /// <returns>The CSV text and the number of data rows written.</returns>
    public static (string Csv, int RowCount) Render(IOeeReport report, string tenant)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);

        var builder = new StringBuilder();
        builder.Append(Header).Append('\n');

        var rows = 0;
        foreach (var machine in report.Machines(tenant))
        {
            foreach (var stat in report.ForMachine(tenant, machine))
            {
                builder
                    .Append(machine).Append(',')
                    .Append(stat.Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(',')
                    .Append(stat.SampleCount.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(stat.AverageOee.ToString("0.####", CultureInfo.InvariantCulture)).Append(',')
                    .Append(stat.MinOee.ToString("0.####", CultureInfo.InvariantCulture)).Append(',')
                    .Append(stat.MaxOee.ToString("0.####", CultureInfo.InvariantCulture)).Append('\n');
                rows++;
            }
        }

        return (builder.ToString(), rows);
    }
}
