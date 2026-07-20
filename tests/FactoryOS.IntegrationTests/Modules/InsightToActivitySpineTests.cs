using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Activity;
using FactoryOS.Plugins.Activity.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// The AI-insight spine over the real bus, zero inter-module references: the Insight agent re-enters an AI-generated
/// root-cause hypothesis as <see cref="InsightGenerated"/>, which the Activity Feed folds into a per-tenant,
/// newest-first "Insight" line without ever referencing the agent or the AI layer. Redelivery of the same insight
/// does not double the entry. `InsightGenerated → activity feed`.
/// </summary>
public sealed class InsightToActivitySpineTests
{
    [Fact]
    public async Task An_insight_lands_on_the_activity_feed_once()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBus();

        new ActivityPlugin().ConfigureServices(services);

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();
        var feed = provider.GetRequiredService<IActivityFeed>();

        var insight = new InsightGenerated
        {
            Tenant = "acme",
            TriggerType = "QualityAlertRaised",
            Subject = "line-1 / widget",
            Insight = "Defect rate rose after the die change; verify die alignment.",
            Model = "reasoning",
            GeneratedAt = DateTimeOffset.UnixEpoch,
            SourceEventId = Guid.NewGuid(),
        };
        await bus.PublishAsync(insight);
        await bus.PublishAsync(insight); // redelivery, same event id — must not double the entry

        var entry = Assert.Single(feed.Recent("acme", 10));
        Assert.Equal("Insight", entry.Category);
        Assert.Contains("line-1", entry.Headline, StringComparison.Ordinal);
        Assert.Contains("die alignment", entry.Headline, StringComparison.Ordinal);
    }
}
