using FactoryOS.Gateway.Endpoints;
using FactoryOS.Gateway.Tenancy;
using FactoryOS.Plugins.Notification.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace FactoryOS.Plugins.Notification.Api;

/// <summary>
/// The Notification outbox read API — exposes the per-tenant history of dispatched notifications over the gateway at
/// <c>/m/notification/*</c>, mounted from the manifest key. A UI or an operator reads what was routed (channel,
/// transport, priority, subject, action, when) without referencing the transport connectors. It reads only; the
/// outbox is written solely by the module's dispatch handlers. The tenant is taken from the ambient
/// <see cref="ITenantContext"/> the gateway resolves at the edge, never re-parsed per route.
/// </summary>
internal sealed class NotificationApi : IModuleApi
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 200;

    private readonly INotificationOutbox _outbox;

    public NotificationApi(INotificationOutbox outbox) => _outbox = outbox;

    public string ModuleKey => NotificationPlugin.PluginKey;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/outbox", ([FromServices] ITenantContext context, int? max) =>
            {
                var limit = Math.Clamp(max ?? DefaultPageSize, 1, MaxPageSize);
                var records = _outbox.ForTenant(context.Tenant);
                var page = records.Count > limit ? records.Take(limit).ToArray() : records;
                return Results.Ok(new OutboxResponse(context.Tenant, page));
            })
            .RequireTenant()
            .WithName("GetNotificationOutbox");
    }
}

/// <summary>A tenant's dispatched-notification history.</summary>
/// <param name="Tenant">The tenant the notifications belong to.</param>
/// <param name="Notifications">The dispatched notifications, newest first.</param>
internal sealed record OutboxResponse(string Tenant, IReadOnlyList<NotificationRecord> Notifications);
