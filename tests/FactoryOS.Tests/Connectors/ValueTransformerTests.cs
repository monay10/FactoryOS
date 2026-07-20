using FactoryOS.Connectors.Transforms;

namespace FactoryOS.Tests.Connectors;

public sealed class ValueTransformerTests
{
    private readonly IValueTransformer _transformer = new ValueTransformer();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Absent_transform_is_identity(string? name)
    {
        var result = _transformer.Apply(name, "unchanged");

        Assert.True(result.IsSuccess);
        Assert.Equal("unchanged", result.Value);
    }

    [Fact]
    public void Trim_upper_and_lower_reshape_strings()
    {
        Assert.Equal("abc", _transformer.Apply("trim", "  abc  ").Value);
        Assert.Equal("ABC", _transformer.Apply("upper", "abc").Value);
        Assert.Equal("abc", _transformer.Apply("lower", "ABC").Value);
    }

    [Fact]
    public void Decimal_and_int_parse_invariantly()
    {
        Assert.Equal(1250.5m, _transformer.Apply("decimal", "1250.5").Value);
        Assert.Equal(42, _transformer.Apply("int", "42").Value);
    }

    [Fact]
    public void Datetime_parses_to_utc_offset()
    {
        var result = _transformer.Apply("datetime", "2026-07-20T08:30:00Z");

        var value = Assert.IsType<DateTimeOffset>(result.Value);
        Assert.Equal(new DateTimeOffset(2026, 07, 20, 08, 30, 00, TimeSpan.Zero), value);
    }

    [Fact]
    public void Null_value_stays_null_for_typed_transforms()
    {
        Assert.Null(_transformer.Apply("decimal", null).Value);
    }

    [Fact]
    public void Unknown_transform_fails()
    {
        var result = _transformer.Apply("does-not-exist", "x");

        Assert.True(result.IsFailure);
        Assert.Equal("Connector.Transform.Unknown", result.Error.Code);
    }

    [Fact]
    public void Unparseable_value_fails()
    {
        var result = _transformer.Apply("decimal", "not-a-number");

        Assert.True(result.IsFailure);
        Assert.Equal("Connector.Transform.Failed", result.Error.Code);
    }
}
