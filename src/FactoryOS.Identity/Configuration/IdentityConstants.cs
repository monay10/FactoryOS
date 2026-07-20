namespace FactoryOS.Identity.Configuration;

/// <summary>
/// Stable constants for the FactoryOS <b>Identity</b> foundation: configuration section names and the
/// default password-policy, lockout and session values. These are the single source of truth so the
/// options types, the services and the sample configuration never drift apart.
/// </summary>
public static class IdentityConstants
{
    /// <summary>The root configuration section the <see cref="IdentityOptions"/> bind from.</summary>
    public const string ConfigurationSection = "Identity";

    /// <summary>The configuration section the <see cref="PasswordPolicyOptions"/> bind from.</summary>
    public const string PasswordPolicySection = "Identity:PasswordPolicy";

    /// <summary>The configuration section the <see cref="SessionOptions"/> bind from.</summary>
    public const string SessionSection = "Identity:Session";

    /// <summary>The configuration section the JWT options bind from (mirrors the token layer).</summary>
    public const string JwtSection = "Jwt";

    /// <summary>The default minimum password length enforced by the password policy.</summary>
    public const int DefaultMinimumPasswordLength = 8;

    /// <summary>The default number of consecutive failures before an account is locked out.</summary>
    public const int DefaultMaxFailedAccessAttempts = 5;

    /// <summary>The default lockout duration, in minutes, applied once the failure threshold is reached.</summary>
    public const int DefaultLockoutMinutes = 15;

    /// <summary>The default sliding idle timeout, in minutes, after which an untouched session expires.</summary>
    public const int DefaultSessionIdleTimeoutMinutes = 30;

    /// <summary>The default absolute session lifetime, in hours, after which a session always expires.</summary>
    public const int DefaultSessionAbsoluteTimeoutHours = 8;
}
