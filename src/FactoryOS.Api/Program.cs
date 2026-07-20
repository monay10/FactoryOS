using FactoryOS.Api;
using FactoryOS.Gateway.Routing;
using FactoryOS.Gateway.Tenancy;
using FactoryOS.Identity.Seeding;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// The directory holding deployed plugin folders; each subfolder carries a module.json manifest.
// It is absent in a bare host, in which case no modules are loaded and the gateway serves an empty set.
var pluginsRoot = Path.Combine(builder.Environment.ContentRootPath, "plugins");

// Cross-cutting HTTP host foundation: problem details, health, versioning, OpenAPI, localization,
// CORS, compression, HTTP logging and the request middleware. Registered before the module graph so
// it wraps every request the gateway serves.
builder.AddApiHostFoundation();

// Composition root: the API host depends on the Application layer and, for wiring only, the
// Infrastructure composition root. The plugin modules are discovered, loaded and configured here, and
// the gateway exposes their inventory, UI registry and endpoints.
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddPluginModules(pluginsRoot)
    .AddModuleGateway()
    .AddTenantResolution(builder.Configuration.GetSection(TenantResolutionOptions.SectionName).Bind);

var app = builder.Build();

// The host foundation pipeline wraps everything below (exception boundary, correlation, timing,
// logging, localization, CORS, compression).
app.UseApiHostFoundation();

// Optional demo seed: default roles (and a user per role when a password is configured) so a fresh
// deployment has something to log in with. Disabled unless Identity:Seed:Enabled is set.
var seedOptions = app.Services.GetRequiredService<IOptions<IdentitySeedOptions>>().Value;
if (seedOptions.Enabled)
{
    app.Services.GetRequiredService<DefaultIdentitySeeder>().Seed(seedOptions);
}

// Health probes (/health, /health/live, /health/ready), the OpenAPI document and the Swagger UI.
app.MapApiHostFoundation();

// Credential login and refresh-token rotation, wired identically for the host and its integration tests.
app.MapAuthEndpoints();

// Resolves the request's tenant once, at the edge, into the scoped ITenantContext for module endpoints.
app.UseTenantResolution();

// Validates a Bearer access token into the request principal (the Identity → gateway bridge).
app.UseMiddleware<BearerAuthenticationMiddleware>();

// Resolves the caller's permissions into the scoped IPermissionContext, so navigation is filtered by RBAC.
app.UsePermissionResolution();

// Mounts /modules, /modules/ui and every active module's /m/<key>/* endpoints.
app.MapModuleGateway();

app.Run();
