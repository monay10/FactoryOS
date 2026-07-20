using FactoryOS.Gateway.Branding;
using FactoryOS.Gateway.Modules;
using FactoryOS.Gateway.Tenancy;
using FactoryOS.Gateway.Ui;

namespace FactoryOS.Gateway.Routing;

/// <summary>
/// Everything a shell needs on a cold start, in one call: the resolved tenant, the cross-module navigation,
/// and the API discovery catalog. It saves a fresh PWA the round-trips of hitting <c>/tenant</c>,
/// <c>/modules/ui/nav</c> and <c>/modules/api</c> separately — a pure composition of the same providers, so the
/// bootstrap reflects exactly the active plugins and the resolved tenant, never any core-side branching.
/// </summary>
/// <param name="Tenant">The tenant resolved for the request (unresolved is not an error — the shell can prompt).</param>
/// <param name="Branding">The resolved tenant's presentation identity, so the shell themes itself.</param>
/// <param name="Nav">The navigation grouped by section across all active modules.</param>
/// <param name="Apis">The read-API catalog of every active module that declares routes.</param>
internal sealed record ShellBootstrap(
    TenantContextResponse Tenant,
    TenantBranding Branding,
    NavCatalog Nav,
    IReadOnlyList<ModuleApiSummary> Apis);
