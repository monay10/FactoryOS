using FactoryOS.Plugins.Carbon.Domain;

namespace FactoryOS.Tests.Carbon;

public sealed class InMemoryCarbonLedgerTests
{
    private static CarbonSourceKey Key(string tenant = "acme", string source = "meter-1") => new(tenant, source);

    [Fact]
    public void Accrue_accumulates_and_returns_the_running_total()
    {
        var ledger = new InMemoryCarbonLedger();

        Assert.Equal(40m, ledger.Accrue(Key(), 40m));
        Assert.Equal(100m, ledger.Accrue(Key(), 60m));
    }

    [Fact]
    public void Totals_are_isolated_per_tenant_and_source()
    {
        var ledger = new InMemoryCarbonLedger();

        ledger.Accrue(Key("acme", "meter-1"), 40m);
        ledger.Accrue(Key("acme", "meter-2"), 10m);
        ledger.Accrue(Key("other", "meter-1"), 5m);

        Assert.Equal(2, ledger.ForTenant("acme").Count);
        Assert.Equal(5m, Assert.Single(ledger.ForTenant("other")).CumulativeCo2eKg);
        Assert.Empty(ledger.ForTenant("ghost"));
    }
}
