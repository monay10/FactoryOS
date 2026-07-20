using FactoryOS.Contracts.Iot;
using FactoryOS.Iot.Registry;

namespace FactoryOS.Tests.Iot;

public sealed class DeviceRegistryTests
{
    private readonly IDeviceRegistry _registry = new InMemoryDeviceRegistry();

    private static Device Device(string tenant, string id) => new()
    {
        Tenant = tenant,
        DeviceId = id,
        Name = id,
        Tags = [new DeviceTag { Name = "ch1", Metric = "ActivePower", Unit = "kW" }],
    };

    [Fact]
    public void Registers_and_finds_a_device_within_its_tenant()
    {
        _registry.Register(Device("acme", "pm-1"));

        Assert.NotNull(_registry.Find("acme", "pm-1"));
        Assert.Null(_registry.Find("globex", "pm-1"));
    }

    [Fact]
    public void Lists_only_the_devices_of_the_requested_tenant()
    {
        _registry.Register(Device("acme", "pm-1"));
        _registry.Register(Device("acme", "pm-2"));
        _registry.Register(Device("globex", "pm-9"));

        Assert.Equal(2, _registry.ForTenant("acme").Count);
    }

    [Fact]
    public void Re_registering_replaces_the_device()
    {
        _registry.Register(Device("acme", "pm-1"));
        _registry.Register(Device("acme", "pm-1") with { Name = "Renamed" });

        Assert.Equal("Renamed", _registry.Find("acme", "pm-1")!.Name);
        Assert.Single(_registry.ForTenant("acme"));
    }
}
