using FactoryOS.Api.Platform;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Domain.Results;
using FactoryOS.Plugins.Runtime.Domain;
using FactoryOS.Plugins.Runtime.Security;
using FactoryOS.Plugins.Workflow.Audit.Execution;
using Xunit;

namespace FactoryOS.IntegrationTests.Composition;

/// <summary>
/// Exercises the pure helpers behind the mutating <c>/platform</c> management endpoints: resolving a request into
/// a plugin caller, and mapping a domain error onto an HTTP status. These carry the security-relevant rules —
/// management needs a tenant and an identity, and the caller's authority is its JWT permissions — so they are
/// tested directly.
/// </summary>
public sealed class PlatformManagementTests
{
    [Fact]
    public void A_request_with_no_tenant_is_refused_with_400()
    {
        var resolution = PlatformManagement.ResolveCaller(
            tenant: null, unrestricted: false, subject: "ops", permissions: ["plugin.install"]);

        Assert.False(resolution.Ok);
        Assert.Equal(400, resolution.Status);
    }

    [Fact]
    public void A_request_with_no_identity_is_refused_with_401()
    {
        var resolution = PlatformManagement.ResolveCaller(
            tenant: "acme", unrestricted: true, subject: null, permissions: []);

        Assert.False(resolution.Ok);
        Assert.Equal(401, resolution.Status);
    }

    [Fact]
    public void An_authenticated_caller_holds_its_parsed_permissions_and_junk_is_dropped()
    {
        var resolution = PlatformManagement.ResolveCaller(
            tenant: "acme",
            unrestricted: false,
            subject: "ops",
            permissions: ["plugin.install", "not-a-permission", "plugin.start"]);

        Assert.True(resolution.Ok);
        var caller = resolution.Caller!;
        Assert.Equal("acme", caller.Tenant);
        Assert.Equal("ops", caller.Subject);
        Assert.Equal(2, caller.Permissions.Count);
        Assert.True(caller.Holds(PluginPermission.Parse("plugin.install")));
        Assert.True(caller.Holds(PluginPermission.Parse("plugin.start")));
    }

    [Fact]
    public void The_super_admin_wildcard_claim_becomes_a_caller_that_holds_every_permission()
    {
        // The identity layer issues the Administrator role a bare "*". The caller resolved from it must hold every
        // platform permission — not none, which is what dropping the unparsed "*" would leave.
        var resolution = PlatformManagement.ResolveCaller(
            tenant: "acme", unrestricted: false, subject: "admin", permissions: ["*"]);

        Assert.True(resolution.Ok);
        var caller = resolution.Caller!;
        Assert.True(caller.Holds(PluginPermission.Parse("plugin.install")));
        Assert.True(caller.Holds(PluginPermission.Parse("plugin.remove")));
        Assert.True(caller.Holds(PluginPermission.Parse("security.grant")));
    }

    [Fact]
    public void The_default_authorizer_admits_a_super_admin_caller_resolved_from_the_wildcard_claim()
    {
        // End to end: an Administrator's "*" claim, resolved into a caller, passes the live authorizer the host binds
        // (PermissionPluginAuthorizer) for an operation an ordinary viewer could never perform.
        var caller = PlatformManagement.ResolveCaller(
            tenant: "acme", unrestricted: false, subject: "admin", permissions: ["*"]).Caller!;
        var instance = new PluginInstance("acme", "energy", PluginVersion.Parse("1.0.0"));

        var decision = new PermissionPluginAuthorizer().Authorize(caller, instance, PluginPermissions.Remove);

        Assert.True(decision.Allowed);
    }

    [Fact]
    public void A_caller_without_the_wildcard_does_not_hold_permissions_it_was_not_granted()
    {
        // The mapping must not over-grant: an ordinary caller holds only what it was given, nothing more.
        var resolution = PlatformManagement.ResolveCaller(
            tenant: "acme", unrestricted: false, subject: "viewer", permissions: ["plugin.observe"]);

        var caller = resolution.Caller!;
        Assert.True(caller.Holds(PluginPermission.Parse("plugin.observe")));
        Assert.False(caller.Holds(PluginPermission.Parse("plugin.remove")));
    }

    [Fact]
    public void The_audit_export_defaults_to_csv_and_names_the_download_for_the_tenant()
    {
        var rendering = PlatformManagement.ResolveAuditExport("acme", format: null);

        Assert.Equal(AuditExportFormat.Csv, rendering.Format);
        Assert.Equal("text/csv", rendering.ContentType);
        Assert.Equal("audit-acme.csv", rendering.FileName);
    }

    [Theory]
    [InlineData("json")]
    [InlineData("JSON")]
    public void The_audit_export_honours_an_explicit_json_format_case_insensitively(string format)
    {
        var rendering = PlatformManagement.ResolveAuditExport("acme", format);

        Assert.Equal(AuditExportFormat.Json, rendering.Format);
        Assert.Equal("application/json", rendering.ContentType);
        Assert.Equal("audit-acme.json", rendering.FileName);
    }

    [Theory]
    [InlineData("Plugin.Runtime.Unauthenticated", ErrorType.Validation, 401)]
    [InlineData("Plugin.Runtime.Forbidden", ErrorType.Validation, 403)]
    [InlineData("Plugin.Runtime.Disabled", ErrorType.Conflict, 409)]
    [InlineData("Whatever.NotFound", ErrorType.NotFound, 404)]
    [InlineData("Whatever.Invalid", ErrorType.Validation, 400)]
    [InlineData("Whatever.Boom", ErrorType.Failure, 400)]
    public void An_error_maps_to_the_right_http_status(string code, ErrorType type, int expected)
    {
        Assert.Equal(expected, PlatformManagement.StatusFor(new Error(code, "detail", type)));
    }
}
