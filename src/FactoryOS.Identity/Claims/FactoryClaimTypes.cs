namespace FactoryOS.Identity.Claims;

/// <summary>The claim types FactoryOS issues in its access tokens.</summary>
public static class FactoryClaimTypes
{
    /// <summary>The subject (user identifier) claim.</summary>
    public const string Subject = "sub";

    /// <summary>The tenant identifier claim — present on every principal so tenant is always in scope.</summary>
    public const string Tenant = "factoryos:tenant";

    /// <summary>The organization identifier claim.</summary>
    public const string Organization = "factoryos:org";

    /// <summary>The user name claim.</summary>
    public const string UserName = "factoryos:username";

    /// <summary>The email claim.</summary>
    public const string Email = "email";

    /// <summary>A role claim (one per assigned role).</summary>
    public const string Role = "factoryos:role";

    /// <summary>A permission claim (one per effective permission).</summary>
    public const string Permission = "factoryos:permission";
}
