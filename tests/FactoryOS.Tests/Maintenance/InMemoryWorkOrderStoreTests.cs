using FactoryOS.Contracts.StandardModel;
using FactoryOS.Plugins.Maintenance.Domain;

namespace FactoryOS.Tests.Maintenance;

public sealed class InMemoryWorkOrderStoreTests
{
    private static WorkOrder Order(string tenant, string number) =>
        new() { Tenant = tenant, Number = number, Title = "t", Status = "Open" };

    [Fact]
    public void Adding_the_same_number_twice_is_rejected()
    {
        var store = new InMemoryWorkOrderStore();

        Assert.True(store.TryAdd(Order("acme", "WO-1")));
        Assert.False(store.TryAdd(Order("acme", "WO-1"))); // duplicate number
        Assert.Single(store.ForTenant("acme"));
    }

    [Fact]
    public void The_same_number_in_different_tenants_is_allowed()
    {
        var store = new InMemoryWorkOrderStore();

        Assert.True(store.TryAdd(Order("acme", "WO-1")));
        Assert.True(store.TryAdd(Order("other", "WO-1")));
        Assert.Single(store.ForTenant("acme"));
        Assert.Single(store.ForTenant("other"));
    }

    [Fact]
    public void For_an_unknown_tenant_the_list_is_empty()
    {
        Assert.Empty(new InMemoryWorkOrderStore().ForTenant("ghost"));
    }

    [Fact]
    public void Closing_an_open_order_transitions_it_and_reports_the_transition()
    {
        var store = new InMemoryWorkOrderStore();
        store.TryAdd(Order("acme", "WO-1"));

        var outcome = store.Close("acme", "WO-1");

        Assert.Equal(WorkOrderCloseResult.Closed, outcome.Result);
        Assert.Equal(InMemoryWorkOrderStore.ClosedStatus, outcome.WorkOrder!.Status);
        Assert.Equal(InMemoryWorkOrderStore.ClosedStatus, store.ForTenant("acme").Single().Status);
    }

    [Fact]
    public void Closing_an_already_closed_order_is_idempotent()
    {
        var store = new InMemoryWorkOrderStore();
        store.TryAdd(Order("acme", "WO-1"));
        store.Close("acme", "WO-1");

        var outcome = store.Close("acme", "WO-1");

        Assert.Equal(WorkOrderCloseResult.AlreadyClosed, outcome.Result);
        Assert.Equal(InMemoryWorkOrderStore.ClosedStatus, outcome.WorkOrder!.Status);
    }

    [Fact]
    public void Closing_an_unknown_number_reports_not_found()
    {
        var store = new InMemoryWorkOrderStore();
        store.TryAdd(Order("acme", "WO-1"));

        var outcome = store.Close("acme", "WO-404");

        Assert.Equal(WorkOrderCloseResult.NotFound, outcome.Result);
        Assert.Null(outcome.WorkOrder);
    }

    [Fact]
    public void Closing_never_reaches_across_tenants()
    {
        var store = new InMemoryWorkOrderStore();
        store.TryAdd(Order("acme", "WO-1"));

        Assert.Equal(WorkOrderCloseResult.NotFound, store.Close("other", "WO-1").Result);
        Assert.Equal("Open", store.ForTenant("acme").Single().Status);
    }
}
