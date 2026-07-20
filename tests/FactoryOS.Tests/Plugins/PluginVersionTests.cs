using FactoryOS.Contracts.Plugins;

namespace FactoryOS.Tests.Plugins;

public sealed class PluginVersionTests
{
    [Fact]
    public void Parse_reads_all_three_components()
    {
        var version = PluginVersion.Parse("2.5.9");

        Assert.Equal(2, version.Major);
        Assert.Equal(5, version.Minor);
        Assert.Equal(9, version.Patch);
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("1.0.0.0")]
    [InlineData("1.0.x")]
    [InlineData("-1.0.0")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParse_rejects_malformed_input(string? value)
    {
        Assert.False(PluginVersion.TryParse(value, out _));
    }

    [Fact]
    public void Parse_throws_on_malformed_input()
    {
        Assert.Throws<FormatException>(() => PluginVersion.Parse("nope"));
    }

    [Fact]
    public void Ordering_is_by_major_then_minor_then_patch()
    {
        Assert.True(PluginVersion.Parse("1.0.0") < PluginVersion.Parse("1.0.1"));
        Assert.True(PluginVersion.Parse("1.2.0") > PluginVersion.Parse("1.1.9"));
        Assert.True(PluginVersion.Parse("2.0.0") >= PluginVersion.Parse("1.9.9"));
        Assert.True(PluginVersion.Parse("1.0.0") <= PluginVersion.Parse("1.0.0"));
    }

    [Fact]
    public void ToString_round_trips()
    {
        Assert.Equal("3.4.5", PluginVersion.Parse("3.4.5").ToString());
    }
}
