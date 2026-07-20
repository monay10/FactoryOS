using FactoryOS.Shared.Guards;
using FactoryOS.Shared.Identifiers;

namespace FactoryOS.Infrastructure.Configuration;

/// <summary>Infrastructure-layer constants: configuration section name and the defaults options bind against.</summary>
public static class InfrastructureConstants
{
    /// <summary>The configuration section the infrastructure options bind from.</summary>
    public const string ConfigurationSection = "Infrastructure";

    /// <summary>The default cache time-to-live, in seconds, applied when a caller specifies none.</summary>
    public const int DefaultCacheTimeToLiveSeconds = 300;

    /// <summary>The default root, relative to the content root, under which local files are stored.</summary>
    public const string DefaultFileStorageRoot = "storage";

    /// <summary>The default culture used when a request resolves none.</summary>
    public const string DefaultCulture = "en";
}

/// <summary>Bindable options that tune infrastructure-layer behavior.</summary>
public sealed class InfrastructureOptions
{
    /// <summary>Gets or sets the root path under which the local file storage keeps its objects.</summary>
    public string FileStorageRootPath { get; set; } = InfrastructureConstants.DefaultFileStorageRoot;

    /// <summary>Gets or sets the default cache time-to-live applied when a caller specifies none.</summary>
    public TimeSpan DefaultCacheTimeToLive { get; set; } =
        TimeSpan.FromSeconds(InfrastructureConstants.DefaultCacheTimeToLiveSeconds);

    /// <summary>Gets or sets the default culture used when a request resolves none.</summary>
    public string DefaultCulture { get; set; } = InfrastructureConstants.DefaultCulture;
}

/// <summary>
/// The scoped, ambient execution and security context for the current request. The composition edge (for example an
/// API middleware) populates it once via <see cref="Initialize"/>; the <c>Current*</c> services read from it. When it
/// is left uninitialized — background jobs, startup, tests — it represents an anonymous caller with no resolved scope,
/// which is a valid state and never a customer-specific branch.
/// </summary>
public sealed class InfrastructureContext
{
    /// <summary>Initializes a new instance of the <see cref="InfrastructureContext"/> class as an anonymous caller.</summary>
    public InfrastructureContext()
    {
        Permissions = [];
    }

    /// <summary>Gets a value indicating whether the caller is authenticated.</summary>
    public bool IsAuthenticated { get; private set; }

    /// <summary>Gets the caller's user identifier, when authenticated.</summary>
    public UserId? UserId { get; private set; }

    /// <summary>Gets the caller's user name, when authenticated.</summary>
    public string? UserName { get; private set; }

    /// <summary>Gets the permissions the caller holds (may contain wildcard grants).</summary>
    public IReadOnlyCollection<string> Permissions { get; private set; }

    /// <summary>Gets the resolved tenant, or <see langword="null"/> when none was resolved.</summary>
    public string? Tenant { get; private set; }

    /// <summary>Gets the resolved factory scope, when applicable.</summary>
    public FactoryId? FactoryId { get; private set; }

    /// <summary>Gets the resolved plant scope, when applicable.</summary>
    public PlantId? PlantId { get; private set; }

    /// <summary>Gets the resolved work-center scope, when applicable.</summary>
    public WorkCenterId? WorkCenterId { get; private set; }

    /// <summary>Populates the execution context for the current request.</summary>
    /// <param name="tenant">The resolved tenant, if any.</param>
    /// <param name="userId">The authenticated user's identifier, if any.</param>
    /// <param name="userName">The authenticated user's name, if any.</param>
    /// <param name="permissions">The permissions the caller holds.</param>
    /// <param name="factoryId">The resolved factory scope, if any.</param>
    /// <param name="plantId">The resolved plant scope, if any.</param>
    /// <param name="workCenterId">The resolved work-center scope, if any.</param>
    public void Initialize(
        string? tenant,
        UserId? userId,
        string? userName,
        IReadOnlyCollection<string> permissions,
        FactoryId? factoryId = null,
        PlantId? plantId = null,
        WorkCenterId? workCenterId = null)
    {
        Guard.AgainstNull(permissions);

        Tenant = tenant;
        UserId = userId;
        UserName = userName;
        Permissions = permissions;
        IsAuthenticated = userId is not null;
        FactoryId = factoryId;
        PlantId = plantId;
        WorkCenterId = workCenterId;
    }
}
