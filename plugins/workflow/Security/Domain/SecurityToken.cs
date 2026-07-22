namespace FactoryOS.Plugins.Workflow.Security.Domain;

/// <summary>
/// A token the platform issued, as the platform remembers it.
/// <para>
/// This is a <b>reference token</b> model: the handle a caller presents is looked up here, and everything that
/// matters — lifetime, revocation, audience, tenant, the session it is bound to — is read from the record
/// rather than from the bearer's copy. That is a deliberate choice over re-implementing signature
/// verification. The platform already signs and verifies JWTs in <c>FactoryOS.Identity</c>; a second crypto
/// path here would be a second thing to get wrong, and the one property a self-contained signed token cannot
/// give you is the one that matters most in a factory — <b>immediate revocation</b>. A stolen token that stays
/// valid until it expires is not a token anybody can act on.
/// </para>
/// <para>
/// A JWT and this record compose rather than compete: the JWT's <c>jti</c> is the handle, the signature is
/// verified where the key lives, and the answer to "is it still good?" is asked here.
/// </para>
/// </summary>
public sealed class SecurityToken
{
    private readonly List<SecurityClaim> _claims;

    private SecurityToken(
        string handle,
        string subject,
        string tenant,
        string audience,
        string issuer,
        DateTimeOffset issuedOnUtc,
        DateTimeOffset expiresOnUtc,
        string? sessionId,
        IEnumerable<SecurityClaim>? claims)
    {
        Handle = handle;
        Subject = subject;
        Tenant = tenant;
        Audience = audience;
        Issuer = issuer;
        IssuedOnUtc = issuedOnUtc;
        ExpiresOnUtc = expiresOnUtc;
        SessionId = sessionId;
        _claims = claims is null ? [] : [.. claims];
    }

    /// <summary>Gets the handle a caller presents.</summary>
    public string Handle { get; }

    /// <summary>Gets the principal the token was issued for.</summary>
    public string Subject { get; }

    /// <summary>Gets the tenant the token belongs to.</summary>
    public string Tenant { get; }

    /// <summary>Gets who the token was issued for the use of.</summary>
    public string Audience { get; }

    /// <summary>Gets who issued it.</summary>
    public string Issuer { get; }

    /// <summary>Gets when it was issued.</summary>
    public DateTimeOffset IssuedOnUtc { get; }

    /// <summary>Gets when it stops being valid.</summary>
    public DateTimeOffset ExpiresOnUtc { get; }

    /// <summary>Gets the session it is bound to, when it is bound to one.</summary>
    public string? SessionId { get; }

    /// <summary>Gets the claims the token carries.</summary>
    public IReadOnlyList<SecurityClaim> Claims => _claims;

    /// <summary>Gets when the token was revoked, or <see langword="null"/> when it has not been.</summary>
    public DateTimeOffset? RevokedOnUtc { get; private set; }

    /// <summary>Gets why it was revoked, when it was.</summary>
    public string? RevocationReason { get; private set; }

    /// <summary>Records that a token was issued.</summary>
    /// <param name="handle">The handle a caller will present.</param>
    /// <param name="subject">The principal.</param>
    /// <param name="tenant">The tenant.</param>
    /// <param name="audience">Who it is for the use of.</param>
    /// <param name="issuer">Who issued it.</param>
    /// <param name="issuedOnUtc">When.</param>
    /// <param name="lifetime">How long it is good for.</param>
    /// <param name="sessionId">The session it is bound to.</param>
    /// <param name="claims">The claims it carries.</param>
    /// <returns>The token record.</returns>
    public static SecurityToken Issue(
        string handle,
        string subject,
        string tenant,
        string audience,
        string issuer,
        DateTimeOffset issuedOnUtc,
        TimeSpan lifetime,
        string? sessionId = null,
        IEnumerable<SecurityClaim>? claims = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handle);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetime, TimeSpan.Zero);

        return new SecurityToken(
            handle, subject, tenant, audience, issuer, issuedOnUtc, issuedOnUtc + lifetime, sessionId, claims);
    }

    /// <summary>Gets a value indicating whether the token is still good at an instant.</summary>
    /// <param name="nowUtc">The instant.</param>
    /// <returns><see langword="true"/> when it is neither expired nor revoked.</returns>
    public bool IsValid(DateTimeOffset nowUtc) => RevokedOnUtc is null && nowUtc < ExpiresOnUtc;

    /// <summary>Revokes the token. Idempotent: the first revocation is the one that stands.</summary>
    /// <param name="nowUtc">When.</param>
    /// <param name="reason">Why.</param>
    /// <returns><see langword="true"/> when this call is what revoked it.</returns>
    public bool Revoke(DateTimeOffset nowUtc, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (RevokedOnUtc is not null)
        {
            return false;
        }

        RevokedOnUtc = nowUtc;
        RevocationReason = reason;
        return true;
    }
}

/// <summary>What validating a presented token concluded.</summary>
/// <param name="IsValid">Whether the token may be used.</param>
/// <param name="Token">The token record, when one was found.</param>
/// <param name="Reason">Why it may not be used, when it may not.</param>
/// <param name="Detail">A sentence describing the outcome.</param>
public sealed record TokenValidationResult(
    bool IsValid, SecurityToken? Token, SecurityDecisionReason? Reason, string Detail)
{
    /// <summary>Builds a successful validation.</summary>
    /// <param name="token">The token.</param>
    /// <returns>The result.</returns>
    public static TokenValidationResult Valid(SecurityToken token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return new TokenValidationResult(true, token, null, "The token is valid.");
    }

    /// <summary>Builds a failed validation.</summary>
    /// <param name="detail">Why it failed.</param>
    /// <param name="token">The token record, when one was found.</param>
    /// <param name="reason">The decision reason to report.</param>
    /// <returns>The result.</returns>
    public static TokenValidationResult Invalid(
        string detail,
        SecurityToken? token = null,
        SecurityDecisionReason reason = SecurityDecisionReason.TokenNotValid) =>
        new(false, token, reason, detail);
}
