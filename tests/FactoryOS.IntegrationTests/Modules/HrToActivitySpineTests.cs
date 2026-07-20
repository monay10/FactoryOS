using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Activity;
using FactoryOS.Plugins.Activity.Domain;
using FactoryOS.Plugins.Hr;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// The compliance spine over one bus, two plugins, zero inter-module references: a worker's certification is
/// recorded (<see cref="WorkerCertificationRecorded"/>), a shift is staffed (<see cref="ShiftStaffed"/>), and when
/// the required certification is expired at the shift start the HR module emits <see cref="CertificationGapDetected"/>
/// — which the Activity Feed folds into a per-tenant, newest-first "Compliance" line without ever referencing the HR
/// module. Redelivery of the staffing neither re-checks the worker nor doubles the feed entry.
/// `ShiftStaffed → CertificationGapDetected → activity feed`.
/// </summary>
public sealed class HrToActivitySpineTests
{
    [Fact]
    public async Task A_certification_gap_lands_on_the_activity_feed_once()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBus();

        new HrPlugin().ConfigureServices(services);
        new ActivityPlugin().ConfigureServices(services);

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();
        var feed = provider.GetRequiredService<IActivityFeed>();

        var shiftStart = DateTimeOffset.UnixEpoch.AddDays(100);

        await bus.PublishAsync(new WorkerCertificationRecorded
        {
            Tenant = "acme",
            WorkerId = "w-1",
            Certification = "Forklift",
            ExpiresAt = shiftStart.AddDays(-2), // expired before the shift
        });

        var staffing = new ShiftStaffed
        {
            Tenant = "acme",
            ShiftId = "s-1",
            WorkerId = "w-1",
            RequiredCertification = "Forklift",
            ShiftStart = shiftStart,
        };
        await bus.PublishAsync(staffing);
        await bus.PublishAsync(staffing); // redelivery, same event id — must not double the entry

        var entry = Assert.Single(feed.Recent("acme", 10));
        Assert.Equal("Compliance", entry.Category);
        Assert.Contains("w-1", entry.Headline, StringComparison.Ordinal);
        Assert.Contains("Forklift", entry.Headline, StringComparison.Ordinal);
    }
}
