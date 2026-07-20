using FactoryOS.Agents.Insight.Domain;
using FactoryOS.Gateway.Endpoints;
using FactoryOS.Gateway.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace FactoryOS.Agents.Insight.Api;

/// <summary>
/// The Insight agent's read API — the AI layer's queryable memory. It exposes the tenant's recent AI-generated
/// insights through the gateway under <c>/m/insight/*</c>, so the declared <c>AI Insights</c> screen, a wall board
/// or another agent can read what the digital worker concluded without replaying the bus. It reads only; the feed
/// is kept current purely by consuming <see cref="Contracts.Events.InsightGenerated"/>. The tenant comes from the
/// ambient <see cref="ITenantContext"/> the gateway resolves at the edge.
/// </summary>
internal sealed class InsightApi : IModuleApi
{
    private const int DefaultMax = 50;

    private readonly IInsightFeed _feed;

    public InsightApi(IInsightFeed feed)
    {
        _feed = feed;
    }

    public string ModuleKey => InsightAgentPlugin.ManifestKey;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/feed", ([FromServices] ITenantContext context, int? max) =>
                Results.Ok(Project(context.Tenant, _feed.Recent(context.Tenant, max ?? DefaultMax))))
            .RequireTenant()
            .WithName("GetInsightFeed");

        endpoints.MapGet("/summary", ([FromServices] ITenantContext context) =>
                Results.Ok(_feed.Summarize(context.Tenant)))
            .RequireTenant()
            .WithName("GetInsightSummary");
    }

    private static InsightFeedResponse Project(string tenant, IReadOnlyList<InsightRecord> records)
    {
        var items = records
            .Select(static r => new InsightView(
                r.TriggerType,
                r.Subject,
                r.Insight,
                r.Model,
                r.GeneratedAt,
                r.SourceEventId))
            .ToList();

        return new InsightFeedResponse(tenant, items);
    }
}

/// <summary>A tenant's recent AI insights, newest first.</summary>
/// <param name="Tenant">The tenant the feed belongs to.</param>
/// <param name="Insights">The recent insights, newest first.</param>
internal sealed record InsightFeedResponse(string Tenant, IReadOnlyList<InsightView> Insights);

/// <summary>One generated insight, flattened for a feed row.</summary>
/// <param name="TriggerType">The trigger event type the insight responds to.</param>
/// <param name="Subject">The human-readable subject of the trigger.</param>
/// <param name="Insight">The AI-authored insight text.</param>
/// <param name="Model">The upstream model that produced it.</param>
/// <param name="GeneratedAt">When the insight was generated.</param>
/// <param name="SourceEventId">The id of the trigger event, for traceability.</param>
internal sealed record InsightView(
    string TriggerType,
    string Subject,
    string Insight,
    string Model,
    DateTimeOffset GeneratedAt,
    Guid SourceEventId);
