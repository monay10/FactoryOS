using FactoryOS.Gateway.Tenancy;
using Microsoft.AspNetCore.Builder;

namespace Microsoft.AspNetCore.Builder;

/// <summary>Pipeline registration for gateway tenant resolution.</summary>
public static class TenantResolutionApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the <see cref="TenantResolutionMiddleware"/> to the request pipeline. Call before mapping the
    /// module gateway so the resolved tenant is available to every module endpoint.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The same <paramref name="app"/> instance, to allow chaining.</returns>
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<TenantResolutionMiddleware>();
    }
}
