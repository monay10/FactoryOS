using FactoryOS.Contracts.Ai;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Brain;
using FactoryOS.Plugins.Brain.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// The Brain read-model spine over the real bus, zero inter-module references: the Brain Query agent re-enters a
/// grounded answer as <see cref="BrainAnswered"/>, which the Brain read-model plugin folds into a per-tenant,
/// newest-first answer log without referencing the agent or the AI layer. Redelivery of the same answer does not
/// double the log entry. `BrainAnswered → brain answer log`.
/// </summary>
public sealed class BrainAnsweredToLogSpineTests
{
    [Fact]
    public async Task An_answer_lands_on_the_log_once()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBus();

        new BrainPlugin().ConfigureServices(services);

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();
        var log = provider.GetRequiredService<IBrainAnswerLog>();

        var answer = new BrainAnswered
        {
            Tenant = "acme",
            Question = "Why did press-1 spike?",
            Answer = "An energy spike was detected on press-1.",
            Model = "fast",
            Citations = [new BrainCitation { Source = "activity/energy/abc", ChunkId = "activity/energy/abc#0", Score = 0.9 }],
            AnsweredAt = DateTimeOffset.UnixEpoch,
            SourceEventId = Guid.NewGuid(),
        };
        await bus.PublishAsync(answer);
        await bus.PublishAsync(answer); // redelivery, same event id — must not double the entry

        var entry = Assert.Single(log.Recent("acme", 10));
        Assert.Equal("Why did press-1 spike?", entry.Question);
        Assert.Equal("activity/energy/abc", Assert.Single(entry.Citations).Source);
    }
}
