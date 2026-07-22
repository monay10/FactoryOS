using FactoryOS.Domain.Abstractions;
using FactoryOS.Plugins.Workflow.Security.Configuration;
using FactoryOS.Plugins.Workflow.Security.Domain;
using FactoryOS.Plugins.Workflow.Security.Persistence;

namespace FactoryOS.Plugins.Workflow.Security.Execution;

/// <summary>What opening a session did.</summary>
/// <param name="Session">The session that was opened.</param>
/// <param name="Displaced">The sessions that were ended to make room for it.</param>
public sealed record SessionCreation(SecuritySession Session, IReadOnlyList<SecuritySession> Displaced);

/// <summary>
/// Opens, renews, ends and cleans up sessions.
/// <para>
/// The concurrent-session limit <b>displaces the oldest session rather than refusing the new one</b>. Refusing
/// would be a denial-of-service somebody could aim at a colleague: fill their quota from a machine you already
/// have, and they can no longer sign in. Displacing puts the cost on the attacker's own session instead, and
/// the person being displaced finds out because their session ends with a reason attached.
/// </para>
/// </summary>
public sealed class SessionManager
{
    private readonly ISessionRepository _sessions;
    private readonly SecurityEngineOptions _options;
    private readonly IDateTimeProvider _clock;

    /// <summary>Initializes a new instance of the <see cref="SessionManager"/> class.</summary>
    /// <param name="sessions">The session store.</param>
    /// <param name="options">The engine options.</param>
    /// <param name="clock">The clock.</param>
    public SessionManager(
        ISessionRepository sessions, SecurityEngineOptions options, IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        _sessions = sessions;
        _options = options;
        _clock = clock;
    }

    /// <summary>Opens a session, displacing the principal's oldest sessions if it is at its limit.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="subject">The principal.</param>
    /// <param name="networkAddress">Where it was opened from.</param>
    /// <returns>The session and anything displaced to make room.</returns>
    public SessionCreation Create(string tenant, string subject, string? networkAddress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        var now = _clock.UtcNow;
        var displaced = new List<SecuritySession>();

        // Expiries are evaluated on read, so anything already past its clock is retired here rather than
        // counting against the limit — a limit that counted dead sessions would lock somebody out for the
        // length of their absolute lifetime.
        var open = _sessions.ForSubject(tenant, subject)
            .Where(session => Retire(session, now) is null)
            .ToList();

        for (var index = 0; index <= open.Count - _options.MaxConcurrentSessions; index++)
        {
            var oldest = open[index];
            if (oldest.End(now, SessionEndReason.Displaced))
            {
                displaced.Add(oldest);
            }
        }

        var session = SecuritySession.Create(
            Guid.NewGuid().ToString("N"),
            subject,
            tenant,
            now,
            _options.SessionIdleTimeout,
            _options.SessionAbsoluteLifetime,
            networkAddress);

        _sessions.Add(session);
        return new SessionCreation(session, displaced);
    }

    /// <summary>
    /// Finds a session and, if it is still alive, slides its idle window forward.
    /// </summary>
    /// <param name="sessionId">The session.</param>
    /// <returns>The session when it is alive; <see langword="null"/> when it is unknown or finished.</returns>
    public SecuritySession? Renew(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var session = _sessions.Find(sessionId);
        if (session is null)
        {
            return null;
        }

        var now = _clock.UtcNow;
        if (Retire(session, now) is not null)
        {
            return null;
        }

        return session.Renew(now, _options.SessionIdleTimeout) ? session : null;
    }

    /// <summary>Gets a session if it is alive right now, without renewing it.</summary>
    /// <param name="sessionId">The session.</param>
    /// <returns>The session, or <see langword="null"/> when it is unknown or finished.</returns>
    public SecuritySession? FindActive(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var session = _sessions.Find(sessionId);
        return session is not null && Retire(session, _clock.UtcNow) is null ? session : null;
    }

    /// <summary>Ends a session.</summary>
    /// <param name="sessionId">The session.</param>
    /// <returns>The session when this call ended it; <see langword="null"/> when it was already finished.</returns>
    public SecuritySession? Revoke(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var session = _sessions.Find(sessionId);
        return session is not null && session.End(_clock.UtcNow, SessionEndReason.Revoked) ? session : null;
    }

    /// <summary>Ends every open session a principal holds — a sign-out everywhere.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="subject">The principal.</param>
    /// <returns>The sessions this call ended.</returns>
    public IReadOnlyList<SecuritySession> RevokeAll(string tenant, string subject)
    {
        var now = _clock.UtcNow;
        return _sessions.ForSubject(tenant, subject)
            .Where(session => session.End(now, SessionEndReason.Revoked))
            .ToArray();
    }

    /// <summary>
    /// Retires every session in a tenant that has passed one of its clocks. Expiry is evaluated on read
    /// anyway, so this changes no decision — it exists so the store's records say what actually happened
    /// rather than leaving sessions that look open forever.
    /// </summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The sessions this call retired, with the reason each ended.</returns>
    public IReadOnlyList<(SecuritySession Session, SessionEndReason Reason)> RetireExpired(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);

        var now = _clock.UtcNow;
        var retired = new List<(SecuritySession, SessionEndReason)>();

        foreach (var session in _sessions.ForTenant(tenant).Where(session => session.EndedOnUtc is null))
        {
            if (session.InactiveReason(now) is { } reason && session.End(now, reason))
            {
                retired.Add((session, reason));
            }
        }

        return retired;
    }

    /// <summary>Gets a principal's open sessions.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="subject">The principal.</param>
    /// <returns>The sessions, oldest first.</returns>
    public IReadOnlyList<SecuritySession> ActiveSessions(string tenant, string subject)
    {
        var now = _clock.UtcNow;
        return _sessions.ForSubject(tenant, subject)
            .Where(session => Retire(session, now) is null)
            .ToArray();
    }

    // Ends a session that has passed a clock and reports why, so a caller never has to ask twice.
    private static SessionEndReason? Retire(SecuritySession session, DateTimeOffset now)
    {
        if (session.EndedOnUtc is not null)
        {
            return session.EndReason;
        }

        if (session.InactiveReason(now) is not { } reason)
        {
            return null;
        }

        session.End(now, reason);
        return reason;
    }
}
