using FactoryOS.Configuration.Model;
using FactoryOS.Domain.Results;

namespace FactoryOS.Configuration.Validation;

/// <summary>Validates the structural integrity of a <see cref="TenantConfiguration"/>.</summary>
public interface ITenantConfigurationValidator
{
    /// <summary>Validates a tenant configuration.</summary>
    /// <param name="configuration">The configuration to validate.</param>
    /// <returns>A successful result, or a failure describing the first invalid rule.</returns>
    Result Validate(TenantConfiguration configuration);
}
