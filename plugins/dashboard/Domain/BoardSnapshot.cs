namespace FactoryOS.Plugins.Dashboard.Domain;

/// <summary>
/// An immutable, point-in-time view of one tenant's operations board — the machine OEE tiles and the recent
/// alert feed — ready to hand to a wall dashboard or PWA. A pure read model; querying it never mutates it.
/// </summary>
/// <param name="Tenant">The tenant this snapshot is for.</param>
/// <param name="Machines">Latest OEE per machine, ordered by machine id.</param>
/// <param name="RecentAlerts">The live alert feed, newest first, bounded by the configured capacity.</param>
/// <param name="CriticalAlertCount">How many of <paramref name="RecentAlerts"/> are <see cref="AlertLevels.Critical"/>.</param>
public sealed record BoardSnapshot(
    string Tenant,
    IReadOnlyList<OeeTile> Machines,
    IReadOnlyList<AlertTile> RecentAlerts,
    int CriticalAlertCount)
{
    /// <summary>An empty board for a tenant nothing has been recorded for yet.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>A snapshot with no tiles and no alerts.</returns>
    public static BoardSnapshot Empty(string tenant) => new(tenant, [], [], 0);
}
