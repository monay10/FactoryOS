using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FactoryOS.Api.Platform;
using FactoryOS.Domain.Results;
using FactoryOS.Gateway.Security;
using FactoryOS.Gateway.Tenancy;
using FactoryOS.Identity.Claims;
using FactoryOS.Plugins.Runtime.Domain;
using FactoryOS.Plugins.Runtime.Execution;
using Microsoft.AspNetCore.Http;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// The mutating <c>/platform</c> management surface: install a plugin for a tenant from a discovered package, then
/// walk its lifecycle (load, start, stop, suspend, resume, unload, update, roll back, remove), and manage its
/// grants, configuration and enabled state. Every call runs on behalf of one authenticated caller in one tenant;
/// the caller carries the permissions the gateway resolved from the JWT, and the runtime authorizes each
/// operation against them. Reading platform state is the separate, open <c>PlatformObservabilityEndpoints</c>.
/// </summary>
public static class PlatformManagementEndpoints
{
    /// <summary>Maps the mutating <c>/platform</c> management endpoints onto the web application.</summary>
    /// <param name="app">The web application.</param>
    /// <returns>The same <see cref="WebApplication"/> instance, to allow chaining.</returns>
    public static WebApplication MapPlatformManagement(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // The packages available to install, discovered from the host's package root.
        app.MapGet("/platform/packages", (HttpContext http, ITenantContext tenants, IPermissionContext perms,
            IPluginRuntime runtime) =>
            WithCaller(http, tenants, perms, _ => Task.FromResult(Results.Ok(
                runtime.Discover().Packages.Select(package => new PackageView(
                    package.Key, package.Version.ToString(), package.IsSigned))))));

        // Install a discovered package for the tenant, with the permissions the tenant grants it.
        app.MapPost("/platform/plugins", (InstallRequest request, HttpContext http, ITenantContext tenants,
            IPermissionContext perms, IPluginRuntime runtime) =>
            WithCaller(http, tenants, perms, async caller =>
            {
                var package = runtime.Discover().Packages.FirstOrDefault(candidate =>
                    candidate.Key == request.Key
                    && (string.IsNullOrWhiteSpace(request.Version) || candidate.Version.ToString() == request.Version));

                if (package is null)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status404NotFound,
                        title: "Unknown package",
                        detail: $"No discoverable package '{request.Key}'"
                            + (string.IsNullOrWhiteSpace(request.Version) ? "." : $" at version '{request.Version}'."));
                }

                var result = await runtime.InstallAsync(caller, package, PlatformManagement.ParseGrants(request.Grants));
                return Respond(result, instance => new PluginActionView(
                    instance.PluginKey, instance.Version.ToString(), instance.Status.ToString()));
            }));

        MapLifecycle(app, "load", (runtime, caller, key) => runtime.Lifecycle.LoadAsync(caller, key));
        MapLifecycle(app, "start", (runtime, caller, key) => runtime.Lifecycle.StartAsync(caller, key));
        MapLifecycle(app, "stop", (runtime, caller, key) => runtime.Lifecycle.StopAsync(caller, key));
        MapLifecycle(app, "resume", (runtime, caller, key) => runtime.Lifecycle.ResumeAsync(caller, key));
        MapLifecycle(app, "unload", (runtime, caller, key) => runtime.Lifecycle.UnloadAsync(caller, key));
        MapLifecycle(app, "rollback", (runtime, caller, key) => runtime.Updates.RollbackAsync(caller, key));

        // Suspend keeps the plugin loaded but refuses new work; it carries a reason for the audit trail.
        app.MapPost("/platform/plugins/{key}/suspend", (string key, SuspendRequest? request, HttpContext http,
            ITenantContext tenants, IPermissionContext perms, IPluginRuntime runtime) =>
            WithCaller(http, tenants, perms, async caller =>
            {
                var reason = string.IsNullOrWhiteSpace(request?.Reason)
                    ? "Suspended via the platform management API."
                    : request!.Reason!;
                return Respond(await runtime.Lifecycle.SuspendAsync(caller, key, reason));
            }));

        // Update to a newer discovered version of the same plugin.
        app.MapPost("/platform/plugins/{key}/update", (string key, UpdateRequest request, HttpContext http,
            ITenantContext tenants, IPermissionContext perms, IPluginRuntime runtime) =>
            WithCaller(http, tenants, perms, async caller =>
            {
                var package = runtime.Discover().Packages.FirstOrDefault(candidate =>
                    candidate.Key == key && candidate.Version.ToString() == request.Version);

                if (package is null)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status404NotFound,
                        title: "Unknown package",
                        detail: $"No discoverable package '{key}' at version '{request.Version}'.");
                }

                return Respond(await runtime.Updates.UpdateAsync(caller, package));
            }));

        // Remove the plugin from the tenant (the package is retained for other tenants).
        app.MapDelete("/platform/plugins/{key}", (string key, HttpContext http, ITenantContext tenants,
            IPermissionContext perms, IPluginRuntime runtime) =>
            WithCaller(http, tenants, perms, async caller =>
                Respond(await runtime.Lifecycle.RemoveAsync(caller, key))));

        // Switch a plugin on or off for the tenant.
        app.MapPost("/platform/plugins/{key}/enabled", (string key, EnabledRequest request, HttpContext http,
            ITenantContext tenants, IPermissionContext perms, IPluginRuntime runtime) =>
            WithCaller(http, tenants, perms, caller =>
                Task.FromResult(Respond(runtime.Configuration.SetEnabled(caller, key, request.Enabled)))));

        // Narrow or widen the permissions the tenant grants the plugin.
        app.MapPost("/platform/plugins/{key}/grant", (string key, GrantRequest request, HttpContext http,
            ITenantContext tenants, IPermissionContext perms, IPluginRuntime runtime) =>
            WithCaller(http, tenants, perms, caller =>
                Task.FromResult(Respond(
                    runtime.Configuration.Grant(caller, key, PlatformManagement.ParseGrants(request.Grants))))));

        // Replace the plugin's tenant configuration values.
        app.MapPut("/platform/plugins/{key}/config", (string key, ConfigureRequest request, HttpContext http,
            ITenantContext tenants, IPermissionContext perms, IPluginRuntime runtime) =>
            WithCaller(http, tenants, perms, caller =>
                Task.FromResult(Respond(runtime.Configuration.Configure(caller, key, request.Values)))));

        return app;
    }

    private static void MapLifecycle(
        WebApplication app, string verb, Func<IPluginRuntime, PluginCaller, string, Task<Result>> operation)
    {
        app.MapPost($"/platform/plugins/{{key}}/{verb}", (string key, HttpContext http, ITenantContext tenants,
            IPermissionContext perms, IPluginRuntime runtime) =>
            WithCaller(http, tenants, perms, async caller => Respond(await operation(runtime, caller, key))));
    }

    // Every management call runs on behalf of one authenticated caller in one tenant. A request with no tenant is
    // refused (400); one with no identity is refused (401). The caller carries the JWT-resolved permissions, which
    // the runtime authorizes each operation against.
    private static async Task<IResult> WithCaller(
        HttpContext http, ITenantContext tenants, IPermissionContext perms, Func<PluginCaller, Task<IResult>> action)
    {
        var tenant = tenants.TryGetTenant(out var resolved) ? resolved : null;
        var subject = http.User.FindFirst(FactoryClaimTypes.Subject)?.Value
            ?? http.User.FindFirst(FactoryClaimTypes.UserName)?.Value;

        var resolution = PlatformManagement.ResolveCaller(tenant, perms.Unrestricted, subject, [.. perms.Permissions]);
        if (!resolution.Ok)
        {
            return Results.Problem(
                statusCode: resolution.Status, title: "Cannot perform platform management", detail: resolution.Problem);
        }

        return await action(resolution.Caller!);
    }

    private static IResult Respond(Result result) =>
        result.IsSuccess ? Results.NoContent() : Fail(result.Error);

    private static IResult Respond<T>(Result<T> result, Func<T, object> project) =>
        result.IsSuccess ? Results.Ok(project(result.Value)) : Fail(result.Error);

    private static IResult Fail(Error error) => Results.Problem(
        statusCode: PlatformManagement.StatusFor(error), title: error.Code, detail: error.Description);
}

/// <summary>A discoverable package that can be installed.</summary>
/// <param name="Key">The plugin key.</param>
/// <param name="Version">The package version.</param>
/// <param name="Signed">Whether the package carries a signature.</param>
internal sealed record PackageView(string Key, string Version, bool Signed);

/// <summary>The result of an install or update.</summary>
/// <param name="Key">The plugin key.</param>
/// <param name="Version">The installed version.</param>
/// <param name="Status">The instance status.</param>
internal sealed record PluginActionView(string Key, string Version, string Status);

/// <summary>A request to install a discovered package for the tenant.</summary>
/// <param name="Key">The plugin key to install.</param>
/// <param name="Version">The version to install; the only discoverable version when omitted.</param>
/// <param name="Grants">The permissions the tenant grants the plugin.</param>
internal sealed record InstallRequest(string Key, string? Version, IReadOnlyList<string>? Grants);

/// <summary>A request to update a plugin to a newer discovered version.</summary>
/// <param name="Version">The target version.</param>
internal sealed record UpdateRequest(string Version);

/// <summary>A request to suspend a plugin.</summary>
/// <param name="Reason">Why the plugin is being suspended; a default is used when omitted.</param>
internal sealed record SuspendRequest(string? Reason);

/// <summary>A request to switch a plugin on or off for the tenant.</summary>
/// <param name="Enabled">Whether the plugin is enabled.</param>
internal sealed record EnabledRequest(bool Enabled);

/// <summary>A request to set the permissions the tenant grants the plugin.</summary>
/// <param name="Grants">The granted permissions.</param>
internal sealed record GrantRequest(IReadOnlyList<string>? Grants);

/// <summary>A request to replace the plugin's tenant configuration values.</summary>
/// <param name="Values">The configuration values.</param>
internal sealed record ConfigureRequest(Dictionary<string, string?> Values);
