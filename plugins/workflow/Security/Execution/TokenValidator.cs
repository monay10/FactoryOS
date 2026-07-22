using FactoryOS.Domain.Abstractions;
using FactoryOS.Plugins.Workflow.Security.Configuration;
using FactoryOS.Plugins.Workflow.Security.Domain;
using FactoryOS.Plugins.Workflow.Security.Persistence;

namespace FactoryOS.Plugins.Workflow.Security.Execution;

/// <summary>
/// Issues token records and answers whether a presented token may still be used.
/// <para>
/// Validation asks five questions in a fixed order — is it known, is it revoked, has it expired, is it for this
/// audience, is the session behind it still alive — and stops at the first "no" with a reason. What it does
/// <b>not</b> do is verify a signature: the platform already signs and verifies its JWTs where the signing key
/// lives, and a second implementation here would be a second thing to get wrong. The two compose — the token's
/// <c>jti</c> is the handle looked up here.
/// </para>
/// <para>
/// Checking the bound session on every use is what makes revocation mean something. A self-contained token
/// stays valid until it expires no matter what you do; a token whose session was ended stops working on the
/// next request, which is the behaviour anybody who has just realised a laptop was stolen actually needs.
/// </para>
/// </summary>
public sealed class TokenValidator
{
    private readonly ITokenRepository _tokens;
    private readonly SessionManager _sessions;
    private readonly SecurityEngineOptions _options;
    private readonly IDateTimeProvider _clock;

    /// <summary>Initializes a new instance of the <see cref="TokenValidator"/> class.</summary>
    /// <param name="tokens">The token store.</param>
    /// <param name="sessions">The session manager.</param>
    /// <param name="options">The engine options.</param>
    /// <param name="clock">The clock.</param>
    public TokenValidator(
        ITokenRepository tokens,
        SessionManager sessions,
        SecurityEngineOptions options,
        IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        _tokens = tokens;
        _sessions = sessions;
        _options = options;
        _clock = clock;
    }

    /// <summary>Records an issued token.</summary>
    /// <param name="subject">The principal it is for.</param>
    /// <param name="tenant">The tenant.</param>
    /// <param name="issuer">Who issued it.</param>
    /// <param name="sessionId">The session it is bound to.</param>
    /// <param name="claims">The claims it carries.</param>
    /// <param name="audience">Who it is for the use of; defaults to the configured audience.</param>
    /// <param name="lifetime">How long it is good for; defaults to the configured lifetime.</param>
    /// <returns>The token record.</returns>
    public SecurityToken Issue(
        string subject,
        string tenant,
        string issuer,
        string? sessionId = null,
        IEnumerable<SecurityClaim>? claims = null,
        string? audience = null,
        TimeSpan? lifetime = null)
    {
        var token = SecurityToken.Issue(
            Guid.NewGuid().ToString("N"),
            subject,
            tenant,
            audience ?? _options.DefaultAudience,
            issuer,
            _clock.UtcNow,
            lifetime ?? _options.TokenLifetime,
            sessionId,
            claims);

        _tokens.Add(token);
        return token;
    }

    /// <summary>Answers whether a presented token may be used.</summary>
    /// <param name="handle">The handle the caller presented.</param>
    /// <param name="tenant">The tenant the request names, when it names one.</param>
    /// <param name="audience">The audience the request is for, when it names one.</param>
    /// <returns>What validation concluded, and why.</returns>
    public TokenValidationResult Validate(string handle, string? tenant = null, string? audience = null)
    {
        if (string.IsNullOrWhiteSpace(handle))
        {
            return TokenValidationResult.Invalid("No token was presented.");
        }

        var token = _tokens.Find(handle);
        if (token is null)
        {
            return TokenValidationResult.Invalid("The token is not one this platform issued.");
        }

        if (token.RevokedOnUtc is not null)
        {
            return TokenValidationResult.Invalid(
                $"The token was revoked: {token.RevocationReason}.", token);
        }

        var now = _clock.UtcNow;
        if (now >= token.ExpiresOnUtc)
        {
            return TokenValidationResult.Invalid($"The token expired at {token.ExpiresOnUtc:O}.", token);
        }

        // A token minted for one tenant must never be usable in another, whatever it claims about itself.
        if (tenant is not null && !string.Equals(token.Tenant, tenant, StringComparison.Ordinal))
        {
            return TokenValidationResult.Invalid(
                $"The token belongs to tenant '{token.Tenant}', not '{tenant}'.",
                token,
                SecurityDecisionReason.TenantMismatch);
        }

        if (audience is not null && !string.Equals(token.Audience, audience, StringComparison.Ordinal))
        {
            return TokenValidationResult.Invalid(
                $"The token was issued for '{token.Audience}', not '{audience}'.", token);
        }

        if (token.SessionId is { } sessionId && _sessions.FindActive(sessionId) is null)
        {
            return TokenValidationResult.Invalid(
                "The session the token is bound to is no longer active.",
                token,
                SecurityDecisionReason.SessionNotActive);
        }

        return TokenValidationResult.Valid(token);
    }

    /// <summary>Revokes a token.</summary>
    /// <param name="handle">The handle.</param>
    /// <param name="reason">Why.</param>
    /// <returns>The token when this call revoked it; <see langword="null"/> when it was unknown or already revoked.</returns>
    public SecurityToken? Revoke(string handle, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handle);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var token = _tokens.Find(handle);
        return token is not null && token.Revoke(_clock.UtcNow, reason) ? token : null;
    }

    /// <summary>Revokes every token bound to a session — what signing out of a device has to mean.</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="reason">Why.</param>
    /// <returns>The tokens this call revoked.</returns>
    public IReadOnlyList<SecurityToken> RevokeForSession(string sessionId, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var now = _clock.UtcNow;
        return _tokens.ForSession(sessionId)
            .Where(token => token.Revoke(now, reason))
            .ToArray();
    }
}
