namespace FactoryOS.Plugins.Workflow.Security.Configuration;

/// <summary>Stable constants for the security engine.</summary>
public static class SecurityConstants
{
    /// <summary>The configuration section the engine options bind from.</summary>
    public const string ConfigurationSection = "Workflow:Security";

    /// <summary>The tenant used when a request carries none.</summary>
    public const string DefaultTenant = "default";

    /// <summary>The culture used when a decision message declares no localization.</summary>
    public const string DefaultCulture = "en";

    /// <summary>The audience a token is issued for when none is named.</summary>
    public const string DefaultAudience = "factoryos";
}

/// <summary>
/// Runtime options for the security engine (namespace <c>Security.Configuration</c>).
/// <para>
/// Note what is <b>not</b> here: there is no switch that turns authorization off, none that makes an allow
/// outrank a deny, and none that permits reaching across tenants. Those are the three settings every
/// authorization system eventually grows and then gets breached through, so they are not settings.
/// </para>
/// </summary>
public sealed record SecurityEngineOptions
{
    /// <summary>Gets how long a session may sit unused before it expires.</summary>
    public TimeSpan SessionIdleTimeout { get; init; } = TimeSpan.FromMinutes(30);

    /// <summary>Gets how long a session may live at most, however often it is used.</summary>
    public TimeSpan SessionAbsoluteLifetime { get; init; } = TimeSpan.FromHours(12);

    /// <summary>
    /// Gets how many sessions one principal may hold at once. When the limit is reached, opening a new session
    /// displaces the oldest rather than being refused — refusing would let anybody lock a colleague out by
    /// filling their quota from a machine they already control.
    /// </summary>
    public int MaxConcurrentSessions { get; init; } = 5;

    /// <summary>Gets how long an issued token is good for by default.</summary>
    public TimeSpan TokenLifetime { get; init; } = TimeSpan.FromHours(1);

    /// <summary>Gets the audience a token is issued for when none is named.</summary>
    public string DefaultAudience { get; init; } = SecurityConstants.DefaultAudience;

    /// <summary>
    /// Gets the window violations are counted over when deciding whether they add up to an incident.
    /// </summary>
    public TimeSpan ViolationWindow { get; init; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Gets how many violations of one kind by one principal raise an incident. One refused request is a
    /// misconfiguration; a run of them is somebody trying doors.
    /// </summary>
    public int IncidentThreshold { get; init; } = 5;

    /// <summary>
    /// Gets a value indicating whether a granted request is recorded as well as a refused one. On by default:
    /// a trail that only records refusals cannot answer "who read this?", which is the question an auditor
    /// actually asks.
    /// </summary>
    public bool RecordGrantedDecisions { get; init; } = true;

    /// <summary>Gets the default culture used when a decision message declares no localization.</summary>
    public string DefaultCulture { get; init; } = SecurityConstants.DefaultCulture;
}
