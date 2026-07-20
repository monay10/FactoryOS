namespace FactoryOS.Identity.Sessions;

/// <summary>
/// A server-side authenticated session bound to a user and tenant. A session is alive while it is neither
/// revoked, past its sliding idle expiry, nor past its absolute expiry — so both an idle and an absolute
/// timeout are honoured.
/// </summary>
public sealed class ApplicationSession
{
    private ApplicationSession(
        Guid id,
        Guid userId,
        Guid tenantId,
        DateTimeOffset createdOnUtc,
        DateTimeOffset idleExpiresOnUtc,
        DateTimeOffset absoluteExpiresOnUtc)
    {
        Id = id;
        UserId = userId;
        TenantId = tenantId;
        CreatedOnUtc = createdOnUtc;
        LastSeenOnUtc = createdOnUtc;
        IdleExpiresOnUtc = idleExpiresOnUtc;
        AbsoluteExpiresOnUtc = absoluteExpiresOnUtc;
    }

    /// <summary>Gets the session identifier.</summary>
    public Guid Id { get; }

    /// <summary>Gets the owning user identifier.</summary>
    public Guid UserId { get; }

    /// <summary>Gets the owning tenant identifier.</summary>
    public Guid TenantId { get; }

    /// <summary>Gets the creation instant.</summary>
    public DateTimeOffset CreatedOnUtc { get; }

    /// <summary>Gets the instant the session was last touched.</summary>
    public DateTimeOffset LastSeenOnUtc { get; private set; }

    /// <summary>Gets the sliding idle-expiry instant, refreshed on each touch.</summary>
    public DateTimeOffset IdleExpiresOnUtc { get; private set; }

    /// <summary>Gets the absolute expiry instant, which is never extended.</summary>
    public DateTimeOffset AbsoluteExpiresOnUtc { get; }

    /// <summary>Gets the revocation instant, if the session has been revoked.</summary>
    public DateTimeOffset? RevokedOnUtc { get; private set; }

    /// <summary>Creates a new session.</summary>
    /// <param name="id">The session identifier.</param>
    /// <param name="userId">The owning user.</param>
    /// <param name="tenantId">The owning tenant.</param>
    /// <param name="createdOnUtc">The creation instant.</param>
    /// <param name="idleExpiresOnUtc">The initial sliding idle expiry.</param>
    /// <param name="absoluteExpiresOnUtc">The absolute expiry.</param>
    /// <returns>The new session.</returns>
    public static ApplicationSession Create(
        Guid id,
        Guid userId,
        Guid tenantId,
        DateTimeOffset createdOnUtc,
        DateTimeOffset idleExpiresOnUtc,
        DateTimeOffset absoluteExpiresOnUtc) =>
        new(id, userId, tenantId, createdOnUtc, idleExpiresOnUtc, absoluteExpiresOnUtc);

    /// <summary>Determines whether the session is alive at the given instant.</summary>
    /// <param name="now">The current instant.</param>
    /// <returns><see langword="true"/> when neither revoked nor expired.</returns>
    public bool IsActive(DateTimeOffset now) =>
        RevokedOnUtc is null && now < IdleExpiresOnUtc && now < AbsoluteExpiresOnUtc;

    /// <summary>Slides the idle window forward, capped at the absolute expiry.</summary>
    /// <param name="now">The current instant.</param>
    /// <param name="idleExpiresOnUtc">The candidate new idle expiry.</param>
    public void Touch(DateTimeOffset now, DateTimeOffset idleExpiresOnUtc)
    {
        LastSeenOnUtc = now;
        IdleExpiresOnUtc = idleExpiresOnUtc < AbsoluteExpiresOnUtc ? idleExpiresOnUtc : AbsoluteExpiresOnUtc;
    }

    /// <summary>Marks the session as revoked (idempotent).</summary>
    /// <param name="now">The revocation instant.</param>
    public void Revoke(DateTimeOffset now) => RevokedOnUtc ??= now;
}
