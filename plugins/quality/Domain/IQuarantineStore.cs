namespace FactoryOS.Plugins.Quality.Domain;

/// <summary>
/// Tracks which production lines are under quarantine, per tenant. Quarantining is idempotent by line id, so a
/// repeat request neither changes state nor re-announces. Tenant-scoped by construction — no read or write crosses
/// tenants. Replaceable by an EF Core-backed store behind the interface.
/// </summary>
public interface IQuarantineStore
{
    /// <summary>Places a line under quarantine for a tenant.</summary>
    /// <param name="tenant">The tenant the line belongs to.</param>
    /// <param name="lineId">The line to quarantine.</param>
    /// <returns><see langword="true"/> if newly quarantined; <see langword="false"/> if it was already quarantined.</returns>
    bool TryQuarantine(string tenant, string lineId);

    /// <summary>Determines whether a line is currently under quarantine for a tenant.</summary>
    /// <param name="tenant">The tenant the line belongs to.</param>
    /// <param name="lineId">The line to check.</param>
    /// <returns><see langword="true"/> when the line is quarantined.</returns>
    bool IsQuarantined(string tenant, string lineId);
}
