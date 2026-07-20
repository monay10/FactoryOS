using FactoryOS.Configuration.Model;
using FactoryOS.Domain.Results;

namespace FactoryOS.Configuration.Validation;

/// <summary>Default <see cref="ITenantConfigurationValidator"/>. Validation is fail-fast.</summary>
public sealed class TenantConfigurationValidator : ITenantConfigurationValidator
{
    /// <inheritdoc />
    public Result Validate(TenantConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (string.IsNullOrWhiteSpace(configuration.TenantId))
        {
            return Result.Failure(
                Error.Validation("Configuration.Tenant.MissingId", "The tenant configuration is missing 'tenantId'."));
        }

        if (string.IsNullOrWhiteSpace(configuration.Name))
        {
            return Result.Failure(
                Error.Validation("Configuration.Tenant.MissingName", "The tenant configuration is missing 'name'."));
        }

        var duplicateModule = FirstDuplicateKey(configuration.Modules.Select(module => module.Key));
        if (duplicateModule is not null)
        {
            return Result.Failure(
                Error.Conflict("Configuration.Tenant.DuplicateModule", $"Duplicate module key '{duplicateModule}'."));
        }

        var duplicatePlugin = FirstDuplicateKey(configuration.Plugins.Select(plugin => plugin.Key));
        if (duplicatePlugin is not null)
        {
            return Result.Failure(
                Error.Conflict("Configuration.Tenant.DuplicatePlugin", $"Duplicate plugin key '{duplicatePlugin}'."));
        }

        if (configuration.Branding is not null && string.IsNullOrWhiteSpace(configuration.Branding.DisplayName))
        {
            return Result.Failure(
                Error.Validation("Configuration.Tenant.InvalidBranding", "Branding requires a non-empty display name."));
        }

        if (configuration.Localization is not null
            && (string.IsNullOrWhiteSpace(configuration.Localization.Language)
                || string.IsNullOrWhiteSpace(configuration.Localization.TimeZone)))
        {
            return Result.Failure(
                Error.Validation(
                    "Configuration.Tenant.InvalidLocalization", "Localization requires a language and a time zone."));
        }

        return Result.Success();
    }

    private static string? FirstDuplicateKey(IEnumerable<string> keys)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in keys)
        {
            if (!seen.Add(key))
            {
                return key;
            }
        }

        return null;
    }
}
