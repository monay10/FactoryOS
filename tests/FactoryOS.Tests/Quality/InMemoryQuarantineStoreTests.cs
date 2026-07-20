using FactoryOS.Plugins.Quality.Domain;

namespace FactoryOS.Tests.Quality;

public sealed class InMemoryQuarantineStoreTests
{
    [Fact]
    public void Quarantining_a_line_reports_the_transition_then_is_idempotent()
    {
        var store = new InMemoryQuarantineStore();

        Assert.True(store.TryQuarantine("acme", "line-1"));   // newly quarantined
        Assert.False(store.TryQuarantine("acme", "line-1"));  // already held — no transition
        Assert.True(store.IsQuarantined("acme", "line-1"));
    }

    [Fact]
    public void An_unquarantined_line_reads_as_not_quarantined()
    {
        var store = new InMemoryQuarantineStore();
        store.TryQuarantine("acme", "line-1");

        Assert.False(store.IsQuarantined("acme", "line-2"));
    }

    [Fact]
    public void Quarantine_never_crosses_tenants()
    {
        var store = new InMemoryQuarantineStore();
        store.TryQuarantine("acme", "line-1");

        Assert.False(store.IsQuarantined("globex", "line-1"));
        Assert.True(store.TryQuarantine("globex", "line-1")); // same id, different tenant — its own transition
    }
}
