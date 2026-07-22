namespace FactoryOS.Plugins.Workflow.Security.Domain;

/// <summary>
/// Something that should not have happened: a refused request, a failed sign-in, a token that was no longer
/// good, an attempt to reach across tenants.
/// <para>
/// A violation is a <b>fact</b>, recorded whether or not anybody is going to look at it. Deciding what deserves
/// attention is the job of <see cref="SecurityRisk"/> and <see cref="SecurityIncident"/>, and keeping the two
/// apart is what stops a noisy threshold from erasing the record of what actually happened.
/// </para>
/// </summary>
/// <param name="Id">The violation identifier.</param>
/// <param name="Kind">What kind of violation it is.</param>
/// <param name="Tenant">The tenant it happened in.</param>
/// <param name="Subject">Who caused it.</param>
/// <param name="OccurredOnUtc">When.</param>
/// <param name="Description">What happened, in a sentence.</param>
public sealed record SecurityViolation(
    Guid Id,
    SecurityViolationKind Kind,
    string Tenant,
    string Subject,
    DateTimeOffset OccurredOnUtc,
    string Description)
{
    /// <summary>Gets the permission that was asked for, when the violation came from an authorization check.</summary>
    public string? Permission { get; init; }

    /// <summary>Gets the resource that was reached for.</summary>
    public string? Resource { get; init; }

    /// <summary>Gets where the request came from.</summary>
    public string? NetworkAddress { get; init; }

    /// <summary>Gets how much attention it deserves on its own.</summary>
    public SecurityRiskLevel Risk { get; init; } = SecurityRiskLevel.Low;

    /// <summary>Gets the identifiers tying it to the request that caused it.</summary>
    public SecurityCorrelation Correlation { get; init; } = SecurityCorrelation.None;

    /// <summary>Records a violation.</summary>
    /// <param name="kind">What kind.</param>
    /// <param name="tenant">The tenant.</param>
    /// <param name="subject">Who caused it.</param>
    /// <param name="occurredOnUtc">When.</param>
    /// <param name="description">What happened.</param>
    /// <returns>The violation.</returns>
    public static SecurityViolation Of(
        SecurityViolationKind kind,
        string tenant,
        string subject,
        DateTimeOffset occurredOnUtc,
        string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        return new SecurityViolation(Guid.NewGuid(), kind, tenant, subject, occurredOnUtc, description);
    }
}

/// <summary>
/// How seriously a pattern of violations should be taken. Risk is computed from what was recorded, never set
/// by hand: a level somebody typed in is a level that stops matching reality the day after it is typed.
/// </summary>
/// <param name="Level">How much attention the pattern deserves.</param>
/// <param name="Score">The count the level was derived from.</param>
/// <param name="Rationale">Why the level is what it is.</param>
public sealed record SecurityRisk(SecurityRiskLevel Level, int Score, string Rationale)
{
    /// <summary>No risk has been observed.</summary>
    public static SecurityRisk None { get; } = new(SecurityRiskLevel.Informational, 0, "Nothing was observed.");

    /// <summary>
    /// Derives a risk level from how many violations of one kind a principal produced in the window.
    /// <para>
    /// A cross-tenant attempt is treated as critical from the first occurrence, on its own scale. One refused
    /// read is a misconfiguration; one attempt to read another factory's data is not, and averaging the two
    /// into the same counter would let the second hide behind the first.
    /// </para>
    /// </summary>
    /// <param name="kind">The kind of violation.</param>
    /// <param name="count">How many were seen in the window.</param>
    /// <returns>The risk.</returns>
    public static SecurityRisk From(SecurityViolationKind kind, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (count == 0)
        {
            return None;
        }

        if (kind is SecurityViolationKind.CrossTenantAccess)
        {
            return new SecurityRisk(
                SecurityRiskLevel.Critical, count,
                $"{count} attempt(s) to reach across tenants; tenant isolation is never a matter of degree.");
        }

        var level = count switch
        {
            >= 20 => SecurityRiskLevel.Critical,
            >= 10 => SecurityRiskLevel.High,
            >= 5 => SecurityRiskLevel.Medium,
            _ => SecurityRiskLevel.Low,
        };

        return new SecurityRisk(level, count, $"{count} {kind} violation(s) in the window.");
    }
}

/// <summary>
/// A pattern of violations somebody is expected to look at. An incident groups the violations that produced it,
/// so the question "what actually happened?" is answerable from the incident alone.
/// </summary>
public sealed class SecurityIncident
{
    private readonly List<SecurityViolation> _violations;

    private SecurityIncident(
        Guid id,
        string tenant,
        string subject,
        SecurityViolationKind kind,
        SecurityRisk risk,
        DateTimeOffset raisedOnUtc,
        IEnumerable<SecurityViolation> violations)
    {
        Id = id;
        Tenant = tenant;
        Subject = subject;
        Kind = kind;
        Risk = risk;
        RaisedOnUtc = raisedOnUtc;
        _violations = [.. violations];
    }

    /// <summary>Gets the incident identifier.</summary>
    public Guid Id { get; }

    /// <summary>Gets the tenant it belongs to.</summary>
    public string Tenant { get; }

    /// <summary>Gets who it concerns.</summary>
    public string Subject { get; }

    /// <summary>Gets the kind of violation that produced it.</summary>
    public SecurityViolationKind Kind { get; }

    /// <summary>Gets how seriously it should be taken.</summary>
    public SecurityRisk Risk { get; }

    /// <summary>Gets when it was raised.</summary>
    public DateTimeOffset RaisedOnUtc { get; }

    /// <summary>Gets where it is in its life.</summary>
    public SecurityIncidentStatus Status { get; private set; } = SecurityIncidentStatus.Open;

    /// <summary>Gets the violations that produced it.</summary>
    public IReadOnlyList<SecurityViolation> Violations => _violations;

    /// <summary>Gets when it was closed, or <see langword="null"/> while it is open.</summary>
    public DateTimeOffset? ClosedOnUtc { get; private set; }

    /// <summary>Gets what closing it was put down to.</summary>
    public string? Resolution { get; private set; }

    /// <summary>Raises an incident from the violations that produced it.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="subject">Who it concerns.</param>
    /// <param name="kind">The kind of violation.</param>
    /// <param name="risk">How seriously to take it.</param>
    /// <param name="raisedOnUtc">When.</param>
    /// <param name="violations">The violations behind it.</param>
    /// <returns>The incident.</returns>
    public static SecurityIncident Raise(
        string tenant,
        string subject,
        SecurityViolationKind kind,
        SecurityRisk risk,
        DateTimeOffset raisedOnUtc,
        IEnumerable<SecurityViolation> violations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentNullException.ThrowIfNull(risk);
        ArgumentNullException.ThrowIfNull(violations);
        return new SecurityIncident(Guid.NewGuid(), tenant, subject, kind, risk, raisedOnUtc, violations);
    }

    /// <summary>Marks the incident as being looked at.</summary>
    /// <returns><see langword="true"/> when the status changed.</returns>
    public bool Investigate()
    {
        if (Status != SecurityIncidentStatus.Open)
        {
            return false;
        }

        Status = SecurityIncidentStatus.Investigating;
        return true;
    }

    /// <summary>Closes the incident.</summary>
    /// <param name="nowUtc">When.</param>
    /// <param name="resolution">What it was put down to.</param>
    /// <param name="dismissed">Whether it was judged not to be a problem.</param>
    /// <returns><see langword="true"/> when this call is what closed it.</returns>
    public bool Close(DateTimeOffset nowUtc, string resolution, bool dismissed = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resolution);
        if (Status is SecurityIncidentStatus.Resolved or SecurityIncidentStatus.Dismissed)
        {
            return false;
        }

        Status = dismissed ? SecurityIncidentStatus.Dismissed : SecurityIncidentStatus.Resolved;
        ClosedOnUtc = nowUtc;
        Resolution = resolution;
        return true;
    }
}
