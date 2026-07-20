using FactoryOS.Plugins.Hr.Domain;

namespace FactoryOS.Tests.Hr;

public sealed class InMemoryCertificationRegistryTests
{
    private static WorkerKey Key(string tenant = "acme", string worker = "w-1") => new(tenant, worker);

    [Fact]
    public void Records_and_reads_an_expiry()
    {
        var registry = new InMemoryCertificationRegistry();
        var expiry = DateTimeOffset.UnixEpoch.AddYears(1);

        registry.Record(Key(), "Forklift", expiry);

        Assert.Equal(expiry, registry.ExpiryOf(Key(), "Forklift"));
    }

    [Fact]
    public void Recording_is_last_write_wins()
    {
        var registry = new InMemoryCertificationRegistry();
        registry.Record(Key(), "Forklift", DateTimeOffset.UnixEpoch.AddYears(1));
        registry.Record(Key(), "Forklift", DateTimeOffset.UnixEpoch.AddYears(2));

        Assert.Equal(DateTimeOffset.UnixEpoch.AddYears(2), registry.ExpiryOf(Key(), "Forklift"));
    }

    [Fact]
    public void An_unheld_certification_or_unknown_worker_reads_null()
    {
        var registry = new InMemoryCertificationRegistry();
        registry.Record(Key(), "Forklift", DateTimeOffset.UnixEpoch.AddYears(1));

        Assert.Null(registry.ExpiryOf(Key(), "Crane"));         // held Forklift, not Crane
        Assert.Null(registry.ExpiryOf(Key(worker: "w-2"), "Forklift")); // unknown worker
        Assert.Null(registry.ExpiryOf(Key(tenant: "other"), "Forklift")); // other tenant
    }
}
