using FactoryOS.Infrastructure.Caching;
using FactoryOS.Infrastructure.Configuration;
using FactoryOS.Infrastructure.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FactoryOS.Tests.Infrastructure;

public sealed class CacheKeyGeneratorTests
{
    [Fact]
    public void A_prefix_with_no_segments_is_the_key()
    {
        var generator = new CacheKeyGenerator();

        Assert.Equal("oee", generator.Generate("oee"));
    }

    [Fact]
    public void Segments_are_colon_delimited_after_the_prefix()
    {
        var generator = new CacheKeyGenerator();

        Assert.Equal("oee:acme:line-7", generator.Generate("oee", "acme", "line-7"));
    }
}

public sealed class MemoryCacheProviderTests
{
    private static MemoryCacheProvider CreateProvider() =>
        new(new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public async Task A_stored_entry_reads_back()
    {
        var provider = CreateProvider();
        var payload = new byte[] { 1, 2, 3 };

        await provider.SetAsync("k", payload);
        var read = await provider.GetAsync("k");

        Assert.NotNull(read);
        Assert.Equal(payload, read);
    }

    [Fact]
    public async Task A_missing_entry_returns_null()
    {
        var provider = CreateProvider();

        Assert.Null(await provider.GetAsync("absent"));
    }

    [Fact]
    public async Task A_removed_entry_is_gone()
    {
        var provider = CreateProvider();
        await provider.SetAsync("k", [9]);

        await provider.RemoveAsync("k");

        Assert.Null(await provider.GetAsync("k"));
    }
}

public sealed class CacheServiceTests
{
    private sealed record Widget(int Id, string Name);

    private static CacheService CreateService()
    {
        var provider = new MemoryCacheProvider(new MemoryCache(new MemoryCacheOptions()));
        var options = Options.Create(new InfrastructureOptions());
        return new CacheService(provider, new JsonSerializer(), options, NullLogger<CacheService>.Instance);
    }

    [Fact]
    public async Task A_typed_value_round_trips()
    {
        var service = CreateService();
        var widget = new Widget(7, "press");

        await service.SetAsync("w", widget);
        var read = await service.GetAsync<Widget>("w");

        Assert.Equal(widget, read);
    }

    [Fact]
    public async Task GetOrCreate_populates_on_a_miss_then_serves_from_cache()
    {
        var service = CreateService();
        var calls = 0;

        var first = await service.GetOrCreateAsync("w", _ =>
        {
            calls++;
            return Task.FromResult(new Widget(1, "a"));
        });
        var second = await service.GetOrCreateAsync("w", _ =>
        {
            calls++;
            return Task.FromResult(new Widget(2, "b"));
        });

        Assert.Equal(1, calls);
        Assert.Equal(first, second);
        Assert.Equal(1, second!.Id);
    }

    [Fact]
    public async Task A_removed_value_is_a_miss()
    {
        var service = CreateService();
        await service.SetAsync("w", new Widget(1, "a"));

        await service.RemoveAsync("w");

        Assert.Null(await service.GetAsync<Widget>("w"));
    }
}
