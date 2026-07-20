using FactoryOS.Configuration.Model;
using FactoryOS.Domain.Results;

namespace FactoryOS.Configuration.Reading;

/// <summary>A source that can (re)load a tenant configuration on demand.</summary>
public interface ITenantConfigurationSource
{
    /// <summary>Loads the current tenant configuration.</summary>
    /// <returns>The loaded configuration, or a failure describing why it could not be loaded.</returns>
    Result<TenantConfiguration> Load();
}
