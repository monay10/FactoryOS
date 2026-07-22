using FactoryOS.Connectors.Framework.Health;

namespace FactoryOS.Connectors.Runtime.Domain;

/// <summary>
/// The answer to one health question about one connector instance.
/// </summary>
/// <param name="Aspect">Which question was asked.</param>
/// <param name="Status">The answer.</param>
/// <param name="Detail">Why, in a form an operator can act on.</param>
public sealed record ConnectorHealthCheckResult(
    ConnectorHealthAspect Aspect, ConnectorHealthStatus Status, string Detail)
{
    /// <summary>Reports a healthy aspect.</summary>
    /// <param name="aspect">The aspect.</param>
    /// <param name="detail">Why.</param>
    /// <returns>The result.</returns>
    public static ConnectorHealthCheckResult Healthy(ConnectorHealthAspect aspect, string detail) =>
        new(aspect, ConnectorHealthStatus.Healthy, detail);

    /// <summary>Reports an impaired aspect.</summary>
    /// <param name="aspect">The aspect.</param>
    /// <param name="detail">Why.</param>
    /// <returns>The result.</returns>
    public static ConnectorHealthCheckResult Degraded(ConnectorHealthAspect aspect, string detail) =>
        new(aspect, ConnectorHealthStatus.Degraded, detail);

    /// <summary>Reports a failed aspect.</summary>
    /// <param name="aspect">The aspect.</param>
    /// <param name="detail">Why.</param>
    /// <returns>The result.</returns>
    public static ConnectorHealthCheckResult Unhealthy(ConnectorHealthAspect aspect, string detail) =>
        new(aspect, ConnectorHealthStatus.Unhealthy, detail);

    /// <summary>Reports that the aspect could not be determined.</summary>
    /// <param name="aspect">The aspect.</param>
    /// <param name="detail">Why.</param>
    /// <returns>The result.</returns>
    public static ConnectorHealthCheckResult Unknown(ConnectorHealthAspect aspect, string detail) =>
        new(aspect, ConnectorHealthStatus.Unknown, detail);
}

/// <summary>
/// Everything known about one connector instance's health, gathered in one pass.
/// <para>
/// The overall status is <b>derived</b>, and the worst aspect wins. Averaging would let a live instance with
/// an expired credential read as "mostly fine" — which is exactly the reading that gets a shift started on a
/// connector that cannot authenticate.
/// </para>
/// </summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="Instance">The instance key.</param>
/// <param name="CheckedUtc">When the report was taken.</param>
/// <param name="Results">The per-aspect answers.</param>
public sealed record ConnectorHealthReport(
    string Tenant,
    string Instance,
    DateTimeOffset CheckedUtc,
    IReadOnlyList<ConnectorHealthCheckResult> Results)
{
    /// <summary>Gets the overall status: the worst answer any aspect gave.</summary>
    public ConnectorHealthStatus Status
    {
        get
        {
            if (Results.Count == 0)
            {
                return ConnectorHealthStatus.Unknown;
            }

            var worst = ConnectorHealthStatus.Healthy;
            foreach (var result in Results)
            {
                if (Rank(result.Status) > Rank(worst))
                {
                    worst = result.Status;
                }
            }

            return worst;
        }
    }

    /// <summary>Gets the aspects that are not healthy, so an operator sees the problem and not the list.</summary>
    public IReadOnlyList<ConnectorHealthCheckResult> Problems =>
        [.. Results.Where(result => result.Status != ConnectorHealthStatus.Healthy)];

    /// <summary>Finds one aspect's answer.</summary>
    /// <param name="aspect">The aspect.</param>
    /// <returns>The answer, or <see langword="null"/> when the aspect was not checked.</returns>
    public ConnectorHealthCheckResult? For(ConnectorHealthAspect aspect) =>
        Results.FirstOrDefault(result => result.Aspect == aspect);

    private static int Rank(ConnectorHealthStatus status) => status switch
    {
        ConnectorHealthStatus.Healthy => 0,
        ConnectorHealthStatus.Unknown => 1,
        ConnectorHealthStatus.Degraded => 2,
        _ => 3,
    };
}
