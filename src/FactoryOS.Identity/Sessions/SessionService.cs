using FactoryOS.Domain.Abstractions;
using FactoryOS.Domain.Results;
using FactoryOS.Identity.Configuration;
using Microsoft.Extensions.Options;

namespace FactoryOS.Identity.Sessions;

/// <summary>Manages the lifecycle of authenticated sessions: create, validate, touch and revoke.</summary>
public interface ISessionService
{
    /// <summary>Creates and stores a new session for a user, applying the configured timeouts.</summary>
    /// <param name="userId">The owning user.</param>
    /// <param name="tenantId">The owning tenant.</param>
    /// <returns>The new session.</returns>
    ApplicationSession Create(Guid userId, Guid tenantId);

    /// <summary>Validates that a session exists and is still alive.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns>A successful result with the live session, or a failure.</returns>
    Result<ApplicationSession> Validate(Guid sessionId);

    /// <summary>Validates a session and, when sliding expiration is enabled, slides its idle window forward.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns>A successful result with the refreshed session, or a failure.</returns>
    Result<ApplicationSession> Touch(Guid sessionId);

    /// <summary>Revokes a session.</summary>
    /// <param name="sessionId">The session identifier.</param>
    void Revoke(Guid sessionId);

    /// <summary>Revokes every session belonging to a user.</summary>
    /// <param name="userId">The user identifier.</param>
    /// <returns>The number of sessions revoked.</returns>
    int RevokeAllForUser(Guid userId);
}

/// <summary>Default <see cref="ISessionService"/> backed by an <see cref="ISessionStore"/> and the session policy.</summary>
public sealed class SessionService : ISessionService
{
    private static readonly Error NotFound =
        Error.NotFound("Identity.Session.NotFound", "The session was not found.");

    private static readonly Error Inactive =
        Error.Validation("Identity.Session.Inactive", "The session is revoked or expired.");

    private readonly ISessionStore _store;
    private readonly IDateTimeProvider _clock;
    private readonly SessionOptions _options;

    /// <summary>Initializes a new instance of the <see cref="SessionService"/> class.</summary>
    /// <param name="store">The session store.</param>
    /// <param name="clock">The clock used for session lifetimes.</param>
    /// <param name="options">The identity options carrying the session policy.</param>
    public SessionService(ISessionStore store, IDateTimeProvider clock, IOptions<IdentityOptions> options)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(options);

        _store = store;
        _clock = clock;
        _options = options.Value.Session;
    }

    /// <inheritdoc />
    public ApplicationSession Create(Guid userId, Guid tenantId)
    {
        var now = _clock.UtcNow;
        var session = ApplicationSession.Create(
            Guid.NewGuid(),
            userId,
            tenantId,
            now,
            now.AddMinutes(_options.IdleTimeoutMinutes),
            now.AddHours(_options.AbsoluteTimeoutHours));

        _store.Add(session);
        return session;
    }

    /// <inheritdoc />
    public Result<ApplicationSession> Validate(Guid sessionId)
    {
        var session = _store.Find(sessionId);
        if (session is null)
        {
            return Result.Failure<ApplicationSession>(NotFound);
        }

        return session.IsActive(_clock.UtcNow)
            ? session
            : Result.Failure<ApplicationSession>(Inactive);
    }

    /// <inheritdoc />
    public Result<ApplicationSession> Touch(Guid sessionId)
    {
        var validation = Validate(sessionId);
        if (validation.IsFailure)
        {
            return validation;
        }

        if (_options.SlidingExpiration)
        {
            var now = _clock.UtcNow;
            validation.Value.Touch(now, now.AddMinutes(_options.IdleTimeoutMinutes));
        }

        return validation.Value;
    }

    /// <inheritdoc />
    public void Revoke(Guid sessionId) => _store.Find(sessionId)?.Revoke(_clock.UtcNow);

    /// <inheritdoc />
    public int RevokeAllForUser(Guid userId)
    {
        var now = _clock.UtcNow;
        var revoked = 0;
        foreach (var session in _store.FindByUser(userId))
        {
            if (session.RevokedOnUtc is null)
            {
                session.Revoke(now);
                revoked++;
            }
        }

        return revoked;
    }
}
