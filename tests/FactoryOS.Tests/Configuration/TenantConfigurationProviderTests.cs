using FactoryOS.Configuration.Model;
using FactoryOS.Configuration.Providers;
using FactoryOS.Configuration.Reading;
using FactoryOS.Domain.Results;

namespace FactoryOS.Tests.Configuration;

public sealed class TenantConfigurationProviderTests
{
    private sealed class StubSource : ITenantConfigurationSource
    {
        public Result<TenantConfiguration> Next { get; set; } = default!;

        public Result<TenantConfiguration> Load() => Next;
    }

    private static TenantConfiguration Config(string name) => new()
    {
        TenantId = "tenant_001",
        Name = name,
    };

    [Fact]
    public void Initial_load_populates_current()
    {
        var source = new StubSource { Next = Config("v1") };

        var provider = new TenantConfigurationProvider(source);

        Assert.Equal("v1", provider.Current.Name);
    }

    [Fact]
    public void Failed_initial_load_throws()
    {
        var source = new StubSource
        {
            Next = Result.Failure<TenantConfiguration>(Error.NotFound("X", "missing")),
        };

        Assert.Throws<InvalidOperationException>(() => new TenantConfigurationProvider(source));
    }

    [Fact]
    public void Reload_swaps_snapshot_and_raises_changed()
    {
        var source = new StubSource { Next = Config("v1") };
        var provider = new TenantConfigurationProvider(source);
        TenantConfiguration? observed = null;
        provider.Changed += (_, config) => observed = config;

        source.Next = Config("v2");
        var result = provider.Reload();

        Assert.True(result.IsSuccess);
        Assert.Equal("v2", provider.Current.Name);
        Assert.Equal("v2", observed!.Name);
    }

    [Fact]
    public void Failed_reload_keeps_current_and_does_not_raise()
    {
        var source = new StubSource { Next = Config("v1") };
        var provider = new TenantConfigurationProvider(source);
        var raised = false;
        provider.Changed += (_, _) => raised = true;

        source.Next = Result.Failure<TenantConfiguration>(Error.Validation("bad", "nope"));
        var result = provider.Reload();

        Assert.True(result.IsFailure);
        Assert.Equal("v1", provider.Current.Name);
        Assert.False(raised);
    }
}
