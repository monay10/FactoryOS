using System.Globalization;

namespace FactoryOS.Shared.Constants;

/// <summary>Well-known HTTP header names used across FactoryOS.</summary>
public static class HeaderNames
{
    /// <summary>The header the gateway resolves the tenant from at the edge.</summary>
    public const string Tenant = "X-FactoryOS-Tenant";

    /// <summary>The header carrying a caller's permissions (dev/tools path; a signed token is preferred).</summary>
    public const string Permissions = "X-FactoryOS-Permissions";

    /// <summary>The header carrying a correlation identifier across service boundaries.</summary>
    public const string CorrelationId = "X-Correlation-Id";
}

/// <summary>Well-known claim types issued in FactoryOS access tokens.</summary>
public static class ClaimTypeNames
{
    /// <summary>The permission claim the gateway filters navigation and authorizes writes by.</summary>
    public const string Permission = "factoryos:permission";

    /// <summary>The tenant claim identifying the token's tenant.</summary>
    public const string Tenant = "factoryos:tenant";
}

/// <summary>Well-known role names seeded by the identity layer.</summary>
public static class RoleNames
{
    /// <summary>Full access (holds the <c>*</c> permission).</summary>
    public const string Administrator = "Administrator";

    /// <summary>Read access across the plant's operational screens.</summary>
    public const string PlantSupervisor = "PlantSupervisor";

    /// <summary>Energy monitoring and operation.</summary>
    public const string EnergyOperator = "EnergyOperator";

    /// <summary>Quality monitoring and line holds.</summary>
    public const string QualityInspector = "QualityInspector";

    /// <summary>Maintenance work-order handling.</summary>
    public const string MaintenanceTechnician = "MaintenanceTechnician";
}

/// <summary>Well-known permission keys (resource.action) enforced across FactoryOS.</summary>
public static class PermissionNames
{
    /// <summary>The global wildcard granting every permission.</summary>
    public const string All = "*";

    /// <summary>View the operations dashboard.</summary>
    public const string DashboardView = "dashboard.view";

    /// <summary>View maintenance work orders.</summary>
    public const string MaintenanceView = "maintenance.view";

    /// <summary>Close a maintenance work order (a write action).</summary>
    public const string MaintenanceClose = "maintenance.close";

    /// <summary>View quality lines.</summary>
    public const string QualityView = "quality.view";

    /// <summary>Place a quality line under quarantine (a write action).</summary>
    public const string QualityQuarantine = "quality.quarantine";
}

/// <summary>Well-known authorization policy names.</summary>
public static class PolicyNames
{
    /// <summary>Requires an authenticated caller.</summary>
    public const string Authenticated = "factoryos.authenticated";

    /// <summary>Requires a resolved tenant on the request.</summary>
    public const string TenantResolved = "factoryos.tenant-resolved";
}

/// <summary>Stable, machine-readable error codes shared with the exception family.</summary>
public static class ErrorCodes
{
    /// <summary>A generic failure.</summary>
    public const string Failure = "failure";

    /// <summary>An input validation failure.</summary>
    public const string Validation = "validation_failed";

    /// <summary>A resource was not found.</summary>
    public const string NotFound = "not_found";

    /// <summary>An operation conflicts with current state.</summary>
    public const string Conflict = "conflict";

    /// <summary>Authentication is required.</summary>
    public const string Unauthorized = "unauthorized";

    /// <summary>The caller lacks permission.</summary>
    public const string Forbidden = "forbidden";
}

/// <summary>Builders for Redis/read-model cache keys, namespaced per tenant to prevent cross-tenant reads.</summary>
public static class CacheKeys
{
    /// <summary>Builds a tenant-scoped cache key.</summary>
    /// <param name="tenant">The tenant the key belongs to.</param>
    /// <param name="segments">The key segments after the tenant.</param>
    /// <returns>A colon-delimited, tenant-namespaced cache key.</returns>
    public static string ForTenant(string tenant, params ReadOnlySpan<string> segments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        return segments.Length == 0
            ? string.Create(CultureInfo.InvariantCulture, $"factoryos:{tenant}")
            : $"factoryos:{tenant}:{string.Join(':', segments.ToArray())}";
    }
}

/// <summary>Well-known localization resource keys.</summary>
public static class LocalizationKeys
{
    /// <summary>The key for a generic "not found" message.</summary>
    public const string NotFound = "common.not_found";

    /// <summary>The key for a generic validation-failure message.</summary>
    public const string ValidationFailed = "common.validation_failed";

    /// <summary>The key for a generic access-denied message.</summary>
    public const string AccessDenied = "common.access_denied";
}
