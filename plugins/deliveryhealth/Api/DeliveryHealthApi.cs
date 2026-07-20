using FactoryOS.Gateway.Endpoints;
using FactoryOS.Gateway.Tenancy;
using FactoryOS.Plugins.DeliveryHealth.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace FactoryOS.Plugins.DeliveryHealth.Api;

/// <summary>
/// The Delivery Health read API — the first module to contribute HTTP endpoints through the gateway. It exposes the
/// tenant-scoped delivery read model over <c>/m/deliveryhealth/*</c> so an operator, a UI or an AI agent can query
/// transport health and recent failure detail without touching the connectors or the Notification module. It reads
/// only; the model is fed exclusively by <c>NotificationDelivered</c> on the bus. The tenant is taken from the
/// ambient <see cref="ITenantContext"/> the gateway resolves at the edge, never re-parsed per route.
/// </summary>
internal sealed class DeliveryHealthApi : IModuleApi
{
    private readonly IDeliveryHealthStore _store;
    private readonly DeliveryHealthOptions _options;

    public DeliveryHealthApi(IDeliveryHealthStore store, DeliveryHealthOptions options)
    {
        _store = store;
        _options = options;
    }

    public string ModuleKey => DeliveryHealthPlugin.PluginKey;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/health", ([FromServices] ITenantContext context) =>
                Results.Ok(new TransportHealthResponse(context.Tenant, _store.ForTenant(context.Tenant))))
            .RequireTenant()
            .WithName("GetDeliveryHealth");

        endpoints.MapGet("/failures", ([FromServices] ITenantContext context, int? max) =>
            {
                var limit = Math.Clamp(max ?? _options.RecentFailureCapacity, 1, _options.RecentFailureCapacity);
                return Results.Ok(new RecentFailuresResponse(context.Tenant, _store.RecentFailures(context.Tenant, limit)));
            })
            .RequireTenant()
            .WithName("GetDeliveryFailures");
    }
}

/// <summary>A tenant's per-transport delivery tallies.</summary>
/// <param name="Tenant">The tenant the tallies belong to.</param>
/// <param name="Transports">The per-transport health tallies, ordered by transport.</param>
internal sealed record TransportHealthResponse(string Tenant, IReadOnlyList<TransportHealth> Transports);

/// <summary>A tenant's most recent failed deliveries.</summary>
/// <param name="Tenant">The tenant the failures belong to.</param>
/// <param name="Failures">The recent failed deliveries, newest first.</param>
internal sealed record RecentFailuresResponse(string Tenant, IReadOnlyList<DeliveryFailure> Failures);
