namespace FactoryOS.Plugins.Safety.Domain;

/// <summary>
/// Maintains a bounded rolling window of recent incidents per site and returns the count within it. Tenant-scoped
/// through the key. Replaceable by a Redis-backed store behind the interface.
/// </summary>
public interface IIncidentWindowStore
{
    /// <summary>Folds an incident into the site's window and returns the incident count within the window.</summary>
    /// <param name="key">The site the incident belongs to.</param>
    /// <returns>The number of incidents in the window after folding in this one.</returns>
    int Fold(SafetySiteKey key);
}
