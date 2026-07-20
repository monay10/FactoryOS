using FactoryOS.Gateway.Security;
using Microsoft.AspNetCore.Builder;

namespace Microsoft.AspNetCore.Builder;

/// <summary>Pipeline registration for gateway permission resolution.</summary>
public static class PermissionResolutionApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the <see cref="PermissionResolutionMiddleware"/> to the request pipeline. Call before mapping the
    /// module gateway so the resolved permissions are available when navigation is built.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The same <paramref name="app"/> instance, to allow chaining.</returns>
    public static IApplicationBuilder UsePermissionResolution(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<PermissionResolutionMiddleware>();
    }
}
