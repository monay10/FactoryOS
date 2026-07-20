namespace FactoryOS.Identity.Configuration;

/// <summary>
/// The password-policy rules enforced when a credential is set. Bound from
/// <see cref="IdentityConstants.PasswordPolicySection"/>.
/// </summary>
public sealed class PasswordPolicyOptions
{
    /// <summary>Gets or sets the minimum acceptable password length.</summary>
    public int MinimumLength { get; set; } = IdentityConstants.DefaultMinimumPasswordLength;

    /// <summary>Gets or sets a value indicating whether at least one uppercase letter is required.</summary>
    public bool RequireUppercase { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether at least one lowercase letter is required.</summary>
    public bool RequireLowercase { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether at least one digit is required.</summary>
    public bool RequireDigit { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether at least one non-alphanumeric character is required.</summary>
    public bool RequireNonAlphanumeric { get; set; } = true;
}

/// <summary>
/// The account-lockout policy applied after consecutive authentication failures. Bound as the
/// <c>Lockout</c> child of <see cref="IdentityConstants.ConfigurationSection"/>.
/// </summary>
public sealed class LockoutOptions
{
    /// <summary>Gets or sets a value indicating whether lockout is enforced at all.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the number of consecutive failures that triggers a lockout.</summary>
    public int MaxFailedAccessAttempts { get; set; } = IdentityConstants.DefaultMaxFailedAccessAttempts;

    /// <summary>Gets or sets the lockout duration, in minutes.</summary>
    public int LockoutMinutes { get; set; } = IdentityConstants.DefaultLockoutMinutes;
}

/// <summary>
/// The session-lifetime policy. A session expires when either the sliding idle window or the absolute
/// lifetime elapses. Bound from <see cref="IdentityConstants.SessionSection"/>.
/// </summary>
public sealed class SessionOptions
{
    /// <summary>Gets or sets the sliding idle timeout, in minutes, refreshed each time the session is touched.</summary>
    public int IdleTimeoutMinutes { get; set; } = IdentityConstants.DefaultSessionIdleTimeoutMinutes;

    /// <summary>Gets or sets the absolute session lifetime, in hours, which is never extended.</summary>
    public int AbsoluteTimeoutHours { get; set; } = IdentityConstants.DefaultSessionAbsoluteTimeoutHours;

    /// <summary>Gets or sets a value indicating whether touching a session slides the idle window forward.</summary>
    public bool SlidingExpiration { get; set; } = true;
}

/// <summary>
/// The top-level identity foundation options. Bound from <see cref="IdentityConstants.ConfigurationSection"/>;
/// the <see cref="PasswordPolicy"/>, <see cref="Lockout"/> and <see cref="Session"/> children map to their
/// nested sections.
/// </summary>
public sealed class IdentityOptions
{
    /// <summary>Gets or sets a value indicating whether user names must be unique within a tenant.</summary>
    public bool RequireUniqueUserName { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether every registered user must carry a tenant scope.</summary>
    public bool RequireTenantScope { get; set; } = true;

    /// <summary>Gets or sets the password policy.</summary>
    public PasswordPolicyOptions PasswordPolicy { get; set; } = new();

    /// <summary>Gets or sets the account-lockout policy.</summary>
    public LockoutOptions Lockout { get; set; } = new();

    /// <summary>Gets or sets the session-lifetime policy.</summary>
    public SessionOptions Session { get; set; } = new();
}
