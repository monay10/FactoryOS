namespace FactoryOS.Plugins.Dashboard.Domain;

/// <summary>
/// The tenant-scoped operations read-model: the write side (fed by event handlers) records the latest OEE and
/// pushes alerts; the read side (a dashboard) asks for a <see cref="BoardSnapshot"/>. This is the CQRS read
/// model the Experience layer renders, kept current purely by consuming the event bus.
/// </summary>
public interface IOperationsBoard
{
    /// <summary>Records a machine's latest OEE for a tenant, replacing any previous tile for that machine.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="tile">The OEE tile.</param>
    void RecordOee(string tenant, OeeTile tile);

    /// <summary>Pushes an alert onto a tenant's live feed, dropping the oldest if the feed is at capacity.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="alert">The alert tile.</param>
    void PushAlert(string tenant, AlertTile alert);

    /// <summary>Takes an immutable snapshot of a tenant's board, or an empty one if nothing is recorded yet.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The current board snapshot.</returns>
    BoardSnapshot Snapshot(string tenant);
}
