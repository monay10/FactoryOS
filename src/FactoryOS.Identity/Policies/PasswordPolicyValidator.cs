using FactoryOS.Domain.Results;
using FactoryOS.Identity.Configuration;
using Microsoft.Extensions.Options;

namespace FactoryOS.Identity.Policies;

/// <summary>Validates a plaintext password against the configured <see cref="PasswordPolicyOptions"/>.</summary>
public interface IPasswordPolicy
{
    /// <summary>Validates a candidate password.</summary>
    /// <param name="password">The plaintext password to validate.</param>
    /// <returns>A successful result when the password satisfies the policy, or a validation failure.</returns>
    Result Validate(string password);
}

/// <summary>
/// Default <see cref="IPasswordPolicy"/> enforcing minimum length and character-class requirements
/// (uppercase, lowercase, digit, non-alphanumeric) from the configured policy.
/// </summary>
public sealed class PasswordPolicyValidator : IPasswordPolicy
{
    private readonly PasswordPolicyOptions _policy;

    /// <summary>Initializes a new instance of the <see cref="PasswordPolicyValidator"/> class.</summary>
    /// <param name="options">The identity options carrying the password policy.</param>
    public PasswordPolicyValidator(IOptions<IdentityOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _policy = options.Value.PasswordPolicy;
    }

    /// <inheritdoc />
    public Result Validate(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < _policy.MinimumLength)
        {
            return Result.Failure(Error.Validation(
                "Identity.Password.TooShort",
                $"The password must be at least {_policy.MinimumLength} characters long."));
        }

        if (_policy.RequireUppercase && !password.Any(char.IsUpper))
        {
            return Missing("uppercase letter", "Uppercase");
        }

        if (_policy.RequireLowercase && !password.Any(char.IsLower))
        {
            return Missing("lowercase letter", "Lowercase");
        }

        if (_policy.RequireDigit && !password.Any(char.IsDigit))
        {
            return Missing("digit", "Digit");
        }

        if (_policy.RequireNonAlphanumeric && password.All(char.IsLetterOrDigit))
        {
            return Missing("non-alphanumeric character", "NonAlphanumeric");
        }

        return Result.Success();
    }

    private static Result Missing(string requirement, string code) =>
        Result.Failure(Error.Validation(
            $"Identity.Password.Requires{code}",
            $"The password must contain at least one {requirement}."));
}
