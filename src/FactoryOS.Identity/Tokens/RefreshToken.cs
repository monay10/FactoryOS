namespace FactoryOS.Identity.Tokens;

/// <summary>A long-lived refresh token bound to a user, used to obtain new access tokens.</summary>
public sealed class RefreshToken
{
    private RefreshToken(
        string token,
        Guid userId,
        Guid tenantId,
        DateTimeOffset createdOnUtc,
        DateTimeOffset expiresOnUtc)
    {
        Token = token;
        UserId = userId;
        TenantId = tenantId;
        CreatedOnUtc = createdOnUtc;
        ExpiresOnUtc = expiresOnUtc;
    }

    /// <summary>Gets the opaque token value.</summary>
    public string Token { get; }

    /// <summary>Gets the owning user identifier.</summary>
    public Guid UserId { get; }

    /// <summary>Gets the owning tenant identifier.</summary>
    public Guid TenantId { get; }

    /// <summary>Gets the creation instant.</summary>
    public DateTimeOffset CreatedOnUtc { get; }

    /// <summary>Gets the expiry instant.</summary>
    public DateTimeOffset ExpiresOnUtc { get; }

    /// <summary>Gets the revocation instant, if the token has been revoked.</summary>
    public DateTimeOffset? RevokedOnUtc { get; private set; }

    /// <summary>Creates a new refresh token.</summary>
    /// <param name="token">The opaque token value.</param>
    /// <param name="userId">The owning user.</param>
    /// <param name="tenantId">The owning tenant.</param>
    /// <param name="createdOnUtc">The creation instant.</param>
    /// <param name="expiresOnUtc">The expiry instant.</param>
    /// <returns>The new refresh token.</returns>
    public static RefreshToken Create(
        string token,
        Guid userId,
        Guid tenantId,
        DateTimeOffset createdOnUtc,
        DateTimeOffset expiresOnUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return new RefreshToken(token, userId, tenantId, createdOnUtc, expiresOnUtc);
    }

    /// <summary>Determines whether the token is still usable at the given instant.</summary>
    /// <param name="now">The current instant.</param>
    /// <returns><see langword="true"/> when the token is neither revoked nor expired.</returns>
    public bool IsActive(DateTimeOffset now) => RevokedOnUtc is null && now < ExpiresOnUtc;

    /// <summary>Marks the token as revoked.</summary>
    /// <param name="now">The revocation instant.</param>
    public void Revoke(DateTimeOffset now) => RevokedOnUtc ??= now;
}
