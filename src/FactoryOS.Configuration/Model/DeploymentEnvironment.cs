namespace FactoryOS.Configuration.Model;

/// <summary>The deployment environment a tenant runs in.</summary>
public enum DeploymentEnvironment
{
    /// <summary>Local development environment.</summary>
    Development = 0,

    /// <summary>Pre-production staging environment.</summary>
    Staging = 1,

    /// <summary>Live production environment.</summary>
    Production = 2,
}
