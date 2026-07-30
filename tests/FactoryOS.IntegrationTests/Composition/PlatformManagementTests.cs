using FactoryOS.Api.Platform;
using FactoryOS.Domain.Results;
using FactoryOS.Plugins.Runtime.Domain;
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
