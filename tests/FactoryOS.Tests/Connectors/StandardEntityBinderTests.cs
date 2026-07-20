using FactoryOS.Connectors.Binding;
using FactoryOS.Contracts.Connectors;
using FactoryOS.Contracts.StandardModel;

namespace FactoryOS.Tests.Connectors;

public sealed class StandardEntityBinderTests
{
    private readonly IStandardEntityBinder _binder = new StandardEntityBinder();

    private static NormalizedRecord Normalized(string entityType, string key, Dictionary<string, object?> values) =>
        new("acme", "Logo", entityType, key, values);

    [Fact]
    public void Binds_an_inventory_item()
    {
        var record = Normalized("InventoryItem", "MLZ-1", new Dictionary<string, object?>
        {
            ["Sku"] = "MLZ-1",
            ["Name"] = "Steel",
            ["Quantity"] = 12.5m,
            ["Unit"] = "kg",
            ["Location"] = "A-01",
        });

        var result = _binder.Bind(record);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        var item = Assert.IsType<InventoryItem>(result.Value);
        Assert.Equal("acme", item.Tenant);
        Assert.Equal("MLZ-1", item.Sku);
        Assert.Equal("Steel", item.Name);
        Assert.Equal(12.5m, item.Quantity);
        Assert.Equal("kg", item.Unit);
        Assert.Equal("A-01", item.Location);
        Assert.Equal("MLZ-1", item.NaturalKey);
    }

    [Fact]
    public void Binds_a_meter_reading_with_a_timestamp()
    {
        var readingAt = new DateTimeOffset(2026, 07, 20, 08, 00, 00, TimeSpan.Zero);
        var record = Normalized("MeterReading", "m1", new Dictionary<string, object?>
        {
            ["MeterId"] = "m1",
            ["Metric"] = "ActivePower",
            ["Value"] = 3.2m,
            ["Unit"] = "kW",
            ["ReadingAt"] = readingAt,
        });

        var result = _binder.Bind(record);

        var reading = Assert.IsType<MeterReading>(result.Value);
        Assert.Equal("ActivePower", reading.Metric);
        Assert.Equal(3.2m, reading.Value);
        Assert.Equal(readingAt, reading.ReadingAt);
    }

    [Fact]
    public void Fails_for_an_unknown_entity_type()
    {
        var result = _binder.Bind(Normalized("Unknown", "k", []));

        Assert.True(result.IsFailure);
        Assert.Equal("Connector.Bind.UnknownEntity", result.Error.Code);
    }

    [Fact]
    public void Fails_when_a_required_field_is_absent()
    {
        var result = _binder.Bind(Normalized("InventoryItem", "k", new Dictionary<string, object?> { ["Sku"] = "k" }));

        Assert.True(result.IsFailure);
        Assert.Equal("Connector.Bind.MissingField", result.Error.Code);
    }

    [Fact]
    public void Binds_a_directory_user_with_a_boolean_enabled_flag()
    {
        var record = Normalized("DirectoryUser", "asli", new Dictionary<string, object?>
        {
            ["Username"] = "asli",
            ["DisplayName"] = "Aslı Kaya",
            ["Email"] = "asli@example.com",
            ["Enabled"] = false,
        });

        var result = _binder.Bind(record);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        var user = Assert.IsType<DirectoryUser>(result.Value);
        Assert.Equal("asli", user.NaturalKey);
        Assert.Equal("asli@example.com", user.Email);
        Assert.False(user.Enabled);
    }

    [Fact]
    public void Binds_a_directory_user_defaulting_enabled_to_true_when_absent()
    {
        var record = Normalized("DirectoryUser", "deniz", new Dictionary<string, object?>
        {
            ["Username"] = "deniz",
            ["DisplayName"] = "Deniz Ada",
        });

        var user = Assert.IsType<DirectoryUser>(_binder.Bind(record).Value);
        Assert.True(user.Enabled);
        Assert.Null(user.Email);
    }

    [Fact]
    public void Binds_a_directory_group()
    {
        var record = Normalized("DirectoryGroup", "operators", new Dictionary<string, object?>
        {
            ["GroupName"] = "operators",
            ["DisplayName"] = "Operators",
            ["Description"] = "Shift operators",
        });

        var group = Assert.IsType<DirectoryGroup>(_binder.Bind(record).Value);
        Assert.Equal("operators", group.NaturalKey);
        Assert.Equal("Shift operators", group.Description);
    }
}
