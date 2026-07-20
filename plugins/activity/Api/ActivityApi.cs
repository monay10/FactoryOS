using FactoryOS.Gateway.Endpoints;
using FactoryOS.Gateway.Tenancy;
using FactoryOS.Plugins.Activity.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace FactoryOS.Plugins.Activity.Api;

/// <summary>
/// The Activity Feed read API — exposes the per-tenant factory timeline over the gateway at
/// <c>/m/activity/*</c>, mounted purely from the manifest key. A UI, an operator or an AI agent reads the
/// newest-first stream of noteworthy events without referencing any producing module. It reads only; the feed is
/// fed exclusively by integration events on the bus. The tenant is taken from the ambient
/// <see cref="ITenantContext"/> the gateway resolves at the edge, never re-parsed per route.
/// </summary>
internal sealed class ActivityApi : IModuleApi
{
    private readonly IActivityFeed _feed;
    private readonly ActivityOptions _options;

    public ActivityApi(IActivityFeed feed, ActivityOptions options)
    {
        _feed = feed;
        _options = options;
    }

    public string ModuleKey => ActivityPlugin.PluginKey;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/feed", ([FromServices] ITenantContext context, int? max, string? category) =>
            {
                var limit = Math.Clamp(max ?? _options.FeedCapacity, 1, _options.FeedCapacity);
                return Results.Ok(new ActivityFeedResponse(context.Tenant, _feed.Recent(context.Tenant, limit, category)));
            })
            .RequireTenant()
            .WithName("GetActivityFeed");

        endpoints.MapGet("/summary", ([FromServices] ITenantContext context) =>
                Results.Ok(_feed.Summarize(context.Tenant)))
            .RequireTenant()
            .WithName("GetActivitySummary");
    }
}

/// <summary>A tenant's most recent activity entries.</summary>
/// <param name="Tenant">The tenant the entries belong to.</param>
/// <param name="Entries">The recent activity entries, newest first.</param>
internal sealed record ActivityFeedResponse(string Tenant, IReadOnlyList<ActivityEntry> Entries);
