using System.Collections.Generic;
using System.Linq;
using FactoryOS.Domain.Results;
using FactoryOS.Plugins.Runtime.Domain;
using FactoryOS.Plugins.Workflow.Audit.Execution;

namespace FactoryOS.Api.Platform;

// The tenant/caller resolution and error-to-status mapping behind the mutating /platform management endpoints,
// as pure functions so they are testable without the HTTP pipeline. A management call runs on behalf of exactly
// one authenticated caller in exactly one tenant; a caller's authority is the permissions the gateway resolved
// from the JWT, which become the PluginCaller the runtime authorizes against.

/// <summary>The outcome of resolving a request into a plugin caller: either a caller, or a refusal to return.</summary>
/// <param name="Caller">The resolved caller when resolution succeeded; otherwise <see langword="null"/>.</param>
/// <param name="Problem">The refusal detail when resolution failed; otherwise <see langword="null"/>.</param>
/// <param name="Status">The HTTP status to return on refusal (ignored on success).</param>
internal sealed record CallerResolution(PluginCaller? Caller, string? Problem, int Status)
{
    /// <summary>Whether a caller was resolved.</summary>
    public bool Ok => Caller is not null;
}

/// <summary>How an audit export should be rendered and delivered.</summary>
/// <param name="Format">The format the audit engine renders.</param>
/// <param name="ContentType">The HTTP content type to serve it as.</param>
/// <param name="FileName">The download file name offered to the caller.</param>
internal sealed record AuditExportRendering(AuditExportFormat Format, string ContentType, string FileName);

/// <summary>Pure helpers behind the mutating <c>/platform</c> management surface.</summary>
internal static class PlatformManagement
{
    // The identity layer issues a bare "*" super-admin grant (the same convention the gateway's PermissionMatch
    // honours). Its plugin-permission form is the both-segments wildcard, which Grants every permission. This is the
    // one place identity's "*" convention crosses into the runtime's resource.action vocabulary — mapping it here is
    // what lets a super-admin's authority survive into the caller. (A plugin, by contrast, is never implicitly made
    // super-admin: ParseGrants keeps dropping a bare "*", so a plugin must be granted explicit permissions.)
    private static readonly PluginPermission SuperAdmin =
        PluginPermission.Of(PluginPermission.Wildcard, PluginPermission.Wildcard);

    /// <summary>
    /// Resolves a request into a <see cref="PluginCaller"/>. Management requires an authenticated caller in a
    /// tenant: a request with no resolved tenant is refused (400), and one with no identity — an unrestricted
    /// permission context — is refused (401). The caller carries the permissions the gateway parsed from the
    /// JWT; unparseable entries are dropped rather than failing the whole request.
    /// </summary>
    /// <param name="tenant">The request's resolved tenant, or <see langword="null"/> when none was resolved.</param>
    /// <param name="unrestricted">Whether the permission context is unrestricted (i.e. no identity was presented).</param>
    /// <param name="subject">The caller's subject claim, if any.</param>
    /// <param name="permissions">The permission strings the gateway resolved for the request.</param>
    /// <returns>The resolution: a caller, or a refusal.</returns>
    public static CallerResolution ResolveCaller(
        string? tenant, bool unrestricted, string? subject, IReadOnlyCollection<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        if (string.IsNullOrWhiteSpace(tenant))
        {
            return new CallerResolution(
                null, "This endpoint is tenant-scoped; the request did not resolve a tenant.", 400);
        }

        if (unrestricted)
        {
            return new CallerResolution(
                null, "Platform management requires an authenticated caller.", 401);
        }

        var parsed = new List<PluginPermission>();
        foreach (var permission in permissions)
        {
            if (string.Equals(permission?.Trim(), PluginPermission.Wildcard, StringComparison.Ordinal))
            {
                parsed.Add(SuperAdmin);
            }
            else if (PluginPermission.TryParse(permission, out var value))
            {
                parsed.Add(value);
            }
        }

        var caller = PluginCaller.Holding(
            tenant,
            string.IsNullOrWhiteSpace(subject) ? "unknown" : subject,
            [.. parsed]);

        return new CallerResolution(caller, null, 200);
    }

    /// <summary>
    /// Resolves the requested audit-export format into the format, content type and download file name. The format
    /// is chosen case-insensitively; anything other than an explicit <c>json</c> falls back to CSV, the format an
    /// auditor opens in a spreadsheet.
    /// </summary>
    /// <param name="tenant">The tenant whose trail is being exported; names the download file.</param>
    /// <param name="format">The requested format (<c>csv</c> or <c>json</c>), if any.</param>
    /// <returns>The export rendering.</returns>
    public static AuditExportRendering ResolveAuditExport(string tenant, string? format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);

        return string.Equals(format, "json", StringComparison.OrdinalIgnoreCase)
            ? new AuditExportRendering(AuditExportFormat.Json, "application/json", $"audit-{tenant}.json")
            : new AuditExportRendering(AuditExportFormat.Csv, "text/csv", $"audit-{tenant}.csv");
    }

    /// <summary>Parses permission strings into plugin permissions, dropping any that do not parse.</summary>
    /// <param name="permissions">The permission strings to parse.</param>
    /// <returns>The parsed permissions.</returns>
    public static IReadOnlyList<PluginPermission> ParseGrants(IEnumerable<string>? permissions)
    {
        if (permissions is null)
        {
            return [];
        }

        var parsed = new List<PluginPermission>();
        foreach (var permission in permissions)
        {
            if (PluginPermission.TryParse(permission, out var value))
            {
                parsed.Add(value);
            }
        }

        return parsed;
    }

    /// <summary>
    /// Maps a domain error onto an HTTP status. Authentication and authorization refusals from the runtime carry
    /// distinct codes and map to 401/403; everything else follows the error's classification.
    /// </summary>
    /// <param name="error">The error to map.</param>
    /// <returns>The HTTP status code.</returns>
    public static int StatusFor(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return error.Code switch
        {
            "Plugin.Runtime.Unauthenticated" => 401,
            "Plugin.Runtime.Forbidden" => 403,
            _ => error.Type switch
            {
                ErrorType.Validation => 400,
                ErrorType.NotFound => 404,
                ErrorType.Conflict => 409,
                _ => 400,
            },
        };
    }
}
