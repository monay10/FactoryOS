namespace FactoryOS.Contracts.Connectors;

/// <summary>
/// The contract every connector implements. Connectors are the <b>only</b> door to the outside world:
/// ERP, PLC and third-party systems are reached exclusively through this layer. A connector reads raw
/// <see cref="SourceRecord"/>s from its source; it never interprets them or emits vendor dialects onto
/// the bus — normalization to the Standard Model happens downstream, driven by a mapping manifest.
/// </summary>
public interface IConnector
{
    /// <summary>Gets the stable key identifying this connector; it must match the connector manifest.</summary>
    string Key { get; }

    /// <summary>Gets the name of the source system this connector reads (for example <c>Logo</c>).</summary>
    string SourceSystem { get; }

    /// <summary>Reads raw records from the source system for the tenant in <paramref name="context"/>.</summary>
    /// <param name="context">The read context carrying the tenant and any read parameters.</param>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    /// <returns>An asynchronous stream of raw source records.</returns>
    IAsyncEnumerable<SourceRecord> ReadAsync(ConnectorReadContext context, CancellationToken cancellationToken);
}
