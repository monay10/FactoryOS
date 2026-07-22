using System.Globalization;
using FactoryOS.Plugin.Health;

namespace FactoryOS.Plugins.Runtime.Domain;

/// <summary>The answer to one health question about one plugin instance.</summary>
/// <param name="Aspect">Which question was asked.</param>
/// <param name="Status">The answer.</param>
/// <param name="Detail">Why, in a form an operator can act on.</param>
public sealed record PluginHealthCheckResult(
    PluginHealthAspect Aspect, PluginHealthStatus Status, string Detail)
{
    /// <summary>Reports a healthy aspect.</summary>
    /// <param name="aspect">The aspect.</param>
    /// <param name="detail">Why.</param>
    /// <returns>The result.</returns>
    public static PluginHealthCheckResult Healthy(PluginHealthAspect aspect, string detail) =>
        new(aspect, PluginHealthStatus.Healthy, detail);

    /// <summary>Reports an impaired aspect.</summary>
    /// <param name="aspect">The aspect.</param>
    /// <param name="detail">Why.</param>
    /// <returns>The result.</returns>
    public static PluginHealthCheckResult Degraded(PluginHealthAspect aspect, string detail) =>
        new(aspect, PluginHealthStatus.Degraded, detail);

    /// <summary>Reports a failed aspect.</summary>
    /// <param name="aspect">The aspect.</param>
    /// <param name="detail">Why.</param>
    /// <returns>The result.</returns>
    public static PluginHealthCheckResult Unhealthy(PluginHealthAspect aspect, string detail) =>
        new(aspect, PluginHealthStatus.Unhealthy, detail);

    /// <summary>Reports that the aspect could not be determined.</summary>
    /// <param name="aspect">The aspect.</param>
    /// <param name="detail">Why.</param>
    /// <returns>The result.</returns>
    public static PluginHealthCheckResult Unknown(PluginHealthAspect aspect, string detail) =>
        new(aspect, PluginHealthStatus.Unknown, detail);
}

/// <summary>
/// Everything known about one plugin instance's health, gathered in one pass.
/// <para>
/// The overall status is <b>derived</b>, and the worst aspect wins. A plugin can be beating happily while
/// the tenant has revoked a permission its manifest requires, or while the plugin it depends on is stopped;
/// averaging those into "mostly fine" produces exactly the reading that leaves the real fault unfixed.
/// </para>
/// </summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="PluginKey">The plugin.</param>
/// <param name="CheckedUtc">When the report was taken.</param>
/// <param name="Results">The per-aspect answers.</param>
public sealed record PluginHealthReport(
    string Tenant,
    string PluginKey,
    DateTimeOffset CheckedUtc,
    IReadOnlyList<PluginHealthCheckResult> Results)
{
    /// <summary>Gets the overall status: the worst answer any aspect gave.</summary>
    public PluginHealthStatus Status
    {
        get
        {
            if (Results.Count == 0)
            {
                return PluginHealthStatus.Unknown;
            }

            var worst = PluginHealthStatus.Healthy;
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
    public IReadOnlyList<PluginHealthCheckResult> Problems =>
        [.. Results.Where(result => result.Status != PluginHealthStatus.Healthy)];

    /// <summary>Finds one aspect's answer.</summary>
    /// <param name="aspect">The aspect.</param>
    /// <returns>The answer, or <see langword="null"/> when the aspect was not checked.</returns>
    public PluginHealthCheckResult? For(PluginHealthAspect aspect) =>
        Results.FirstOrDefault(result => result.Aspect == aspect);

    /// <inheritdoc />
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Tenant}|{PluginKey}: {Status}");

    private static int Rank(PluginHealthStatus status) => status switch
    {
        PluginHealthStatus.Healthy => 0,
        PluginHealthStatus.Unknown => 1,
        PluginHealthStatus.Degraded => 2,
        _ => 3,
    };
}
