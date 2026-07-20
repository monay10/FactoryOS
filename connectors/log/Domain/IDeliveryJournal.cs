namespace FactoryOS.Connectors.Log.Domain;

/// <summary>
/// The tenant-scoped journal the log transport delivers into — the "log" this connector writes to, and the
/// read model an operator queries to see what left the system.
/// </summary>
public interface IDeliveryJournal
{
    /// <summary>Appends a delivered notification to a tenant's journal.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="record">The delivery record.</param>
    void Record(string tenant, DeliveryRecord record);

    /// <summary>Returns a tenant's delivered notifications, newest first.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The recorded deliveries.</returns>
    IReadOnlyList<DeliveryRecord> ForTenant(string tenant);
}
