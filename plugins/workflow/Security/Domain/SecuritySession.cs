namespace FactoryOS.Plugins.Workflow.Security.Domain;

/// <summary>
/// A server-side session bound to a principal and a tenant.
/// <para>
/// Two clocks run at once, and both matter. The <b>idle</b> window slides forward every time the session is
/// used, so somebody working is not thrown out mid-task. The <b>absolute</b> lifetime never moves, so a session
/// cannot be kept alive forever by a script that touches it — which is exactly what a stolen session would do.
/// A session is alive only while it is inside both and has not been revoked.
/// </para>
/// <para>
/// The lifetime semantics deliberately match the platform's existing <c>FactoryOS.Identity.Sessions
/// .ApplicationSession</c>. Two session models that expired differently would mean an operator was signed in
/// according to one half of the platform and signed out according to the other.
/// </para>
/// </summary>
public sealed class SecuritySession
{
    private SecuritySession(
        string id,
        string subject,
        string tenant,
        DateTimeOffset createdOnUtc,
        DateTimeOffset idleExpiresOnUtc,
        DateTimeOffset absoluteExpiresOnUtc,
        string? networkAddress)
    {
        Id = id;
        Subject = subject;
        Tenant = tenant;
        CreatedOnUtc = createdOnUtc;
        LastSeenOnUtc = createdOnUtc;
        IdleExpiresOnUtc = idleExpiresOnUtc;
        AbsoluteExpiresOnUtc = absoluteExpiresOnUtc;
        NetworkAddress = networkAddress;
    }

    /// <summary>Gets the session identifier.</summary>
    public string Id { get; }

    /// <summary>Gets the principal the session belongs to.</summary>
    public string Subject { get; }

    /// <summary>Gets the tenant the session belongs to.</summary>
    public string Tenant { get; }

    /// <summary>Gets when the session was created.</summary>
    public DateTimeOffset CreatedOnUtc { get; }

    /// <summary>Gets when the session was last used.</summary>
    public DateTimeOffset LastSeenOnUtc { get; private set; }

    /// <summary>Gets the sliding idle expiry, refreshed on each use.</summary>
    public DateTimeOffset IdleExpiresOnUtc { get; private set; }

    /// <summary>Gets the absolute expiry, which is never extended.</summary>
    public DateTimeOffset AbsoluteExpiresOnUtc { get; }

    /// <summary>Gets where the session was opened from, when it is known.</summary>
    public string? NetworkAddress { get; }

    /// <summary>Gets when the session was ended, or <see langword="null"/> while it is still open.</summary>
    public DateTimeOffset? EndedOnUtc { get; private set; }

    /// <summary>Gets why the session ended, or <see langword="null"/> while it is still open.</summary>
    public SessionEndReason? EndReason { get; private set; }

    /// <summary>Creates a session.</summary>
    /// <param name="id">The session identifier.</param>
    /// <param name="subject">The principal.</param>
    /// <param name="tenant">The tenant.</param>
    /// <param name="createdOnUtc">When it was created.</param>
    /// <param name="idleTimeout">How long it may sit idle.</param>
    /// <param name="absoluteLifetime">How long it may live at most.</param>
    /// <param name="networkAddress">Where it was opened from.</param>
    /// <returns>The session.</returns>
    public static SecuritySession Create(
        string id,
        string subject,
        string tenant,
        DateTimeOffset createdOnUtc,
        TimeSpan idleTimeout,
        TimeSpan absoluteLifetime,
        string? networkAddress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(idleTimeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(absoluteLifetime, TimeSpan.Zero);

        var absolute = createdOnUtc + absoluteLifetime;
        var idle = createdOnUtc + idleTimeout;
        return new SecuritySession(
            id, subject, tenant, createdOnUtc, idle < absolute ? idle : absolute, absolute, networkAddress);
    }

    /// <summary>Gets a value indicating whether the session is usable at an instant.</summary>
    /// <param name="nowUtc">The instant.</param>
    /// <returns><see langword="true"/> when it is neither ended nor past either clock.</returns>
    public bool IsActive(DateTimeOffset nowUtc) =>
        EndedOnUtc is null && nowUtc < IdleExpiresOnUtc && nowUtc < AbsoluteExpiresOnUtc;

    /// <summary>Gets why the session is unusable at an instant, or <see langword="null"/> when it is usable.</summary>
    /// <param name="nowUtc">The instant.</param>
    /// <returns>The reason, or <see langword="null"/>.</returns>
    public SessionEndReason? InactiveReason(DateTimeOffset nowUtc)
    {
        if (EndReason is { } ended)
        {
            return ended;
        }

        if (nowUtc >= AbsoluteExpiresOnUtc)
        {
            return SessionEndReason.AbsoluteTimeout;
        }

        return nowUtc >= IdleExpiresOnUtc ? SessionEndReason.IdleTimeout : null;
    }

    /// <summary>
    /// Slides the idle window forward, capped at the absolute expiry. Renewal is refused once the session is
    /// no longer active — a renewal that could resurrect an expired session would make both clocks decorative.
    /// </summary>
    /// <param name="nowUtc">The instant of use.</param>
    /// <param name="idleTimeout">How long it may sit idle from here.</param>
    /// <returns><see langword="true"/> when the session was renewed.</returns>
    public bool Renew(DateTimeOffset nowUtc, TimeSpan idleTimeout)
    {
        if (!IsActive(nowUtc))
        {
            return false;
        }

        var candidate = nowUtc + idleTimeout;
        LastSeenOnUtc = nowUtc;
        IdleExpiresOnUtc = candidate < AbsoluteExpiresOnUtc ? candidate : AbsoluteExpiresOnUtc;
        return true;
    }

    /// <summary>Ends the session. Idempotent: the first reason recorded is the one that stands.</summary>
    /// <param name="nowUtc">When it ended.</param>
    /// <param name="reason">Why.</param>
    /// <returns><see langword="true"/> when this call is what ended it.</returns>
    public bool End(DateTimeOffset nowUtc, SessionEndReason reason)
    {
        if (EndedOnUtc is not null)
        {
            return false;
        }

        EndedOnUtc = nowUtc;
        EndReason = reason;
        return true;
    }
}
