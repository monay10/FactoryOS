using System.Runtime.CompilerServices;
using FactoryOS.Contracts.Connectors;

namespace FactoryOS.Connectors.Sample;

/// <summary>
/// A reference connector that stands in for a Logo ERP stock read. It demonstrates the full connector
/// contract: it yields raw <see cref="SourceRecord"/>s in the source's own dialect (<c>LG_STLINE</c>
/// columns) and leaves all interpretation to the mapping-driven normalizer.
/// </summary>
public sealed class SampleLogoConnector : IConnector
{
    /// <summary>The connector key, matching <c>connector.json</c>.</summary>
    public const string ConnectorKey = "sample-logo";

    /// <inheritdoc />
    public string Key => ConnectorKey;

    /// <inheritdoc />
    public string SourceSystem => "Logo";

    /// <inheritdoc />
    public async IAsyncEnumerable<SourceRecord> ReadAsync(
        ConnectorReadContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // A real connector would query the ERP here; the sample returns a fixed, illustrative batch that
        // includes a repeated SKU to exercise deduplication.
        await Task.CompletedTask.ConfigureAwait(false);

        foreach (var record in BuildBatch())
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return record;
        }
    }

    private static IEnumerable<SourceRecord> BuildBatch()
    {
        yield return Stock("MLZ-001", "  Steel Sheet 2mm ", "1250.5", "kg", "A-01");
        yield return Stock("MLZ-002", "Copper Wire", "300", "m", "A-02");

        // A later read of the same SKU with an updated quantity; the deduplicator keeps this one.
        yield return Stock("MLZ-001", "  Steel Sheet 2mm ", "1180.0", "kg", "A-01");
    }

    private static SourceRecord Stock(string code, string name, string quantity, string unit, string warehouse) =>
        new("LG_STLINE", new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["STOKKODU"] = code,
            ["STOKADI"] = name,
            ["MIKTAR"] = quantity,
            ["BIRIM"] = unit,
            ["DEPO"] = warehouse,
        });
}
