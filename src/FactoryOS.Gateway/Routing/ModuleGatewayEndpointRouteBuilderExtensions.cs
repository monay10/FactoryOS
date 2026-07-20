using System.Reflection;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Gateway.Branding;
using FactoryOS.Gateway.Endpoints;
using FactoryOS.Gateway.Modules;
using FactoryOS.Gateway.Platform;
using FactoryOS.Gateway.Security;
using FactoryOS.Gateway.Store;
using FactoryOS.Gateway.Tenancy;
using FactoryOS.Gateway.Ui;
using FactoryOS.Plugin.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FactoryOS.Gateway.Routing;

/// <summary>
/// Wires the API gateway onto an application's endpoint pipeline: a module inventory endpoint, the UI
/// lazy-load registry, and the dynamic mounting of every active module's endpoints under a reserved
/// per-module prefix.
/// </summary>
public static class ModuleGatewayEndpointRouteBuilderExtensions
{
    /// <summary>The reserved route prefix under which module endpoints are mounted (<c>/m/&lt;key&gt;</c>).</summary>
    public const string ModuleRoutePrefix = "/m";

    /// <summary>
    /// Maps the gateway endpoints and mounts the endpoints of every active module. Modules that are
    /// disabled, failed or unknown are never mounted, so the routable surface reflects exactly the set
    /// of active plugins.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder to map onto.</param>
    /// <returns>The same <paramref name="endpoints"/> instance, to allow chaining.</returns>
    public static IEndpointRouteBuilder MapModuleGateway(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var services = endpoints.ServiceProvider;
        var host = services.GetRequiredService<IPluginHost>();
        var catalog = services.GetRequiredService<IModuleUiCatalogProvider>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("FactoryOS.Gateway.Routing.ModuleGateway");

        endpoints.MapGet("/modules", () => Results.Ok(DescribeModules(host)))
            .WithName("GetModules");

        endpoints.MapGet("/modules/ui", () => Results.Ok(catalog.GetCatalog()))
            .WithName("GetModuleUiCatalog");

        endpoints.MapGet("/modules/ui/nav", ([FromServices] IPermissionContext permissions) =>
                Results.Ok(NavigationPermissionFilter.Apply(catalog.GetNavigation(), permissions)))
            .WithName("GetModuleNavigation");

        endpoints.MapGet("/modules/api", () => Results.Ok(DescribeModuleApis(host)))
            .WithName("GetModuleApiCatalog");

        endpoints.MapGet("/store/plugins", () => Results.Ok(DescribeStore(host)))
            .WithName("GetStoreCatalog");

        endpoints.MapGet("/store/summary", () => Results.Ok(SummarizeStore(host)))
            .WithName("GetStoreSummary");

        endpoints.MapPost("/store/plugins/{key}/enable", ([FromServices] IPluginAdmin admin, string key) =>
                ToggleResult(admin, host, key, enabled: true))
            .WithName("EnablePlugin");

        endpoints.MapPost("/store/plugins/{key}/disable", ([FromServices] IPluginAdmin admin, string key) =>
                ToggleResult(admin, host, key, enabled: false))
            .WithName("DisablePlugin");

        endpoints.MapGet("/system", () => Results.Ok(DescribeSystem(host)))
            .WithName("GetSystemStatus");

        endpoints.MapGet("/tenant", ([FromServices] ITenantContext tenant) =>
                Results.Ok(new TenantContextResponse(tenant.TryGetTenant(out var resolved), resolved)))
            .WithName("GetResolvedTenant");

        endpoints.MapGet("/shell", (
                [FromServices] ITenantContext tenant,
                [FromServices] ITenantBrandingProvider branding,
                [FromServices] IPermissionContext permissions) =>
            {
                var resolved = tenant.TryGetTenant(out var key);
                return Results.Ok(new ShellBootstrap(
                    new TenantContextResponse(resolved, key),
                    resolved ? branding.ForTenant(key!) : new TenantBranding(string.Empty, "FactoryOS"),
                    NavigationPermissionFilter.Apply(catalog.GetNavigation(), permissions),
                    DescribeModuleApis(host)));
            })
            .WithName("GetShellBootstrap");

        MountModules(endpoints, host, services.GetServices<IModuleApi>(), logger);

        return endpoints;
    }

    private static void MountModules(
        IEndpointRouteBuilder endpoints,
        IPluginHost host,
        IEnumerable<IModuleApi> moduleApis,
        ILogger logger)
    {
        var activeKeys = host.Plugins
            .Where(descriptor => descriptor.State is not (PluginState.Disabled or PluginState.Failed))
            .Select(descriptor => descriptor.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var group in moduleApis.GroupBy(api => api.ModuleKey, StringComparer.OrdinalIgnoreCase))
        {
            var key = group.Key;

            if (string.IsNullOrWhiteSpace(key))
            {
                GatewayLog.ModuleApiSkipped(logger, "<none>", "the module API does not declare a module key");
                continue;
            }

            if (!activeKeys.Contains(key))
            {
                GatewayLog.ModuleApiSkipped(logger, key, "no active module with this key is registered");
                continue;
            }

            var prefix = $"{ModuleRoutePrefix}/{key}";
            var moduleGroup = endpoints.MapGroup(prefix);

            foreach (var api in group)
            {
                api.MapEndpoints(moduleGroup);
            }

            GatewayLog.ModuleMounted(logger, key, prefix);
        }
    }

    private static ModuleApiSummary[] DescribeModuleApis(IPluginHost host) =>
        host.Plugins
            .Where(descriptor => descriptor.State is not (PluginState.Disabled or PluginState.Failed))
            .Where(descriptor => descriptor.Manifest.Api.Count > 0)
            .OrderBy(descriptor => descriptor.Key, StringComparer.Ordinal)
            .Select(descriptor => new ModuleApiSummary(
                descriptor.Manifest.Key,
                descriptor.Manifest.Name,
                descriptor.Manifest.Api))
            .ToArray();

    private static StoreCatalog DescribeStore(IPluginHost host)
    {
        // A dependency is satisfied only by an active plugin (disabled/failed plugins offer nothing).
        var activeVersions = host.Plugins
            .Where(descriptor => descriptor.State is not (PluginState.Disabled or PluginState.Failed))
            .GroupBy(descriptor => descriptor.Manifest.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Max(descriptor => descriptor.Manifest.Version), StringComparer.OrdinalIgnoreCase);

        var plugins = host.Plugins
            .OrderBy(descriptor => descriptor.Manifest.Key, StringComparer.Ordinal)
            .Select(descriptor => new StorePlugin(
                descriptor.Manifest.Key,
                descriptor.Manifest.Name,
                descriptor.Manifest.Version.ToString(),
                descriptor.Manifest.Description,
                descriptor.Manifest.Author,
                descriptor.State.ToString(),
                descriptor.Manifest.Provides,
                descriptor.Manifest.Dependencies
                    .Select(dependency => new StoreDependency(
                        dependency.PluginKey,
                        dependency.MinimumVersion.ToString(),
                        activeVersions.TryGetValue(dependency.PluginKey, out var installed) && dependency.IsSatisfiedBy(installed)))
                    .ToArray()))
            .ToArray();

        return new StoreCatalog(plugins);
    }

    private static SystemStatus DescribeSystem(IPluginHost host)
    {
        var active = host.Plugins
            .Where(descriptor => descriptor.State is not (PluginState.Disabled or PluginState.Failed))
            .ToArray();

        var capabilities = active
            .SelectMany(descriptor => descriptor.Manifest.Provides)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(capability => capability, StringComparer.Ordinal)
            .ToArray();

        var eventTypes = active
            .SelectMany(descriptor => descriptor.Manifest.Consumes.Concat(descriptor.Manifest.Emits))
            .Distinct(StringComparer.Ordinal)
            .Count();

        return new SystemStatus(
            PlatformProduct,
            PlatformVersion,
            host.Plugins.Count,
            active.Length,
            SummarizeStore(host).WithUnmetDependencies,
            capabilities,
            eventTypes);
    }

    private static readonly Assembly PlatformAssembly = typeof(ModuleGatewayEndpointRouteBuilderExtensions).Assembly;

    private static readonly string PlatformProduct =
        PlatformAssembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product is { Length: > 0 } product
            ? product
            : "FactoryOS";

    private static readonly string PlatformVersion =
        (PlatformAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? PlatformAssembly.GetName().Version?.ToString()
            ?? "0.0.0")
        .Split('+')[0]; // strip build metadata (e.g. a source-control hash) for a stable display string

    private static IResult ToggleResult(IPluginAdmin admin, IPluginHost host, string key, bool enabled)
    {
        var result = admin.SetEnabled(key, enabled);
        return result.Outcome switch
        {
            PluginAdminOutcome.NotFound => Results.NotFound(result),
            // A failed plugin must be fixed and reloaded before it can be toggled.
            PluginAdminOutcome.Failed => Results.Conflict(result),
            // Changed or Unchanged: return the plugin's refreshed Store entry (dependency health may have shifted).
            _ => Results.Ok(DescribeStore(host).Plugins.First(p => string.Equals(p.Key, result.Key, StringComparison.Ordinal))),
        };
    }

    private static StoreSummary SummarizeStore(IPluginHost host)
    {
        var catalog = DescribeStore(host);

        var byState = catalog.Plugins
            .GroupBy(plugin => plugin.State, StringComparer.Ordinal)
            .Select(group => new StoreStateTally(group.Key, group.Count()))
            .OrderByDescending(tally => tally.Count)
            .ThenBy(tally => tally.State, StringComparer.Ordinal)
            .ToArray();

        var withUnmet = catalog.Plugins.Count(plugin => plugin.Dependencies.Any(dependency => !dependency.Satisfied));

        return new StoreSummary(catalog.Plugins.Count, byState, withUnmet);
    }

    private static ModuleSummary[] DescribeModules(IPluginHost host) =>
        host.Plugins
            .OrderBy(descriptor => descriptor.Key, StringComparer.Ordinal)
            .Select(descriptor => new ModuleSummary(
                descriptor.Manifest.Key,
                descriptor.Manifest.Name,
                descriptor.Manifest.Version.ToString(),
                descriptor.State.ToString(),
                $"{ModuleRoutePrefix}/{descriptor.Key}"))
            .ToArray();
}
