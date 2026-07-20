using Microsoft.AspNetCore.Routing;

namespace FactoryOS.Gateway.Endpoints;

/// <summary>
/// The contract a plugin implements to contribute HTTP endpoints. The API gateway mounts each
/// module's endpoints under a reserved, per-module route prefix (<c>/m/&lt;key&gt;/*</c>), so modules
/// never collide and the core routes to a module purely by its manifest key — never by name.
/// </summary>
public interface IModuleApi
{
    /// <summary>Gets the manifest key of the module these endpoints belong to.</summary>
    string ModuleKey { get; }

    /// <summary>
    /// Maps the module's endpoints onto the supplied builder, which is already scoped to the module's
    /// <c>/m/&lt;key&gt;</c> route group. Handlers should therefore use paths relative to that prefix.
    /// </summary>
    /// <param name="endpoints">The route builder scoped to this module's prefix.</param>
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
