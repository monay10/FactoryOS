namespace FactoryOS.Plugins.DeliveryHealth;

/// <summary>
/// Configuration for the Delivery Health module. The per-transport tallies grow only with the number of distinct
/// transports, so they need no bound; the retained list of recent failure details is bounded by
/// <see cref="RecentFailureCapacity"/> so troubleshooting context cannot grow without limit.
/// </summary>
public sealed record DeliveryHealthOptions
{
    /// <summary>The configuration section this binds to.</summary>
    public const string SectionName = "Modules:DeliveryHealth";

    /// <summary>The maximum number of recent failure details retained per tenant, newest first.</summary>
    public int RecentFailureCapacity { get; init; } = 50;

    /// <summary>
    /// The consecutive-failure streak at which a transport is declared degraded and <c>DeliveryHealthDegraded</c>
    /// is raised — once per crossing, since any success resets the streak. Values below 1 are treated as 1.
    /// </summary>
    public int FailureThreshold { get; init; } = 3;
}
