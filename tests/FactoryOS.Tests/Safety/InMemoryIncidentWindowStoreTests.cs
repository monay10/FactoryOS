using FactoryOS.Plugins.Safety.Domain;

namespace FactoryOS.Tests.Safety;

public sealed class InMemoryIncidentWindowStoreTests
{
    private static SafetySiteKey Key(string tenant = "acme", string site = "site-1") => new(tenant, site);

    [Fact]
    public void Fold_counts_up_to_the_window_size_then_saturates()
    {
        var store = new InMemoryIncidentWindowStore(windowSize: 3);

        Assert.Equal(1, store.Fold(Key()));
        Assert.Equal(2, store.Fold(Key()));
        Assert.Equal(3, store.Fold(Key()));
        Assert.Equal(3, store.Fold(Key())); // saturated at the window size
    }

    [Fact]
    public void Sites_and_tenants_are_isolated()
    {
        var store = new InMemoryIncidentWindowStore(windowSize: 10);

        store.Fold(Key("acme", "site-1"));
        store.Fold(Key("acme", "site-1"));

        Assert.Equal(1, store.Fold(Key("acme", "site-2")));
        Assert.Equal(1, store.Fold(Key("other", "site-1")));
    }
}
