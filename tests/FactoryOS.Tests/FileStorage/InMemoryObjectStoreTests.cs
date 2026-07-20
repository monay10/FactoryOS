using System.Text;
using FactoryOS.Contracts.Storage;
using FactoryOS.Plugins.FileStorage;
using FactoryOS.Plugins.FileStorage.Domain;

namespace FactoryOS.Tests.FileStorage;

public sealed class InMemoryObjectStoreTests
{
    private static InMemoryObjectStore Store(long maxSize = 0) =>
        new(new FileStorageOptions { MaxObjectSizeBytes = maxSize });

    private static StoredObject Object(string tenant, string key, string body, string contentType = "text/plain") => new()
    {
        Tenant = tenant,
        Key = key,
        ContentType = contentType,
        Content = Encoding.UTF8.GetBytes(body),
    };

    [Fact]
    public async Task Put_then_get_round_trips_the_bytes_and_metadata()
    {
        var store = Store();
        await store.PutAsync(Object("acme", "reports/oee.csv", "a,b,c", "text/csv"), CancellationToken.None);

        var got = await store.GetAsync("acme", "reports/oee.csv", CancellationToken.None);

        Assert.NotNull(got);
        Assert.Equal("text/csv", got.ContentType);
        Assert.Equal("a,b,c", Encoding.UTF8.GetString(got.Content.Span));
        Assert.Equal(5, got.Size);
    }

    [Fact]
    public async Task Put_replaces_an_existing_object_with_the_same_key()
    {
        var store = Store();
        await store.PutAsync(Object("acme", "k", "first"), CancellationToken.None);
        await store.PutAsync(Object("acme", "k", "second"), CancellationToken.None);

        var got = await store.GetAsync("acme", "k", CancellationToken.None);
        Assert.Equal("second", Encoding.UTF8.GetString(got!.Content.Span));
    }

    [Fact]
    public async Task Get_returns_null_for_a_missing_object()
    {
        var store = Store();
        Assert.Null(await store.GetAsync("acme", "nope", CancellationToken.None));
    }

    [Fact]
    public async Task Exists_reflects_presence()
    {
        var store = Store();
        await store.PutAsync(Object("acme", "k", "x"), CancellationToken.None);

        Assert.True(await store.ExistsAsync("acme", "k", CancellationToken.None));
        Assert.False(await store.ExistsAsync("acme", "other", CancellationToken.None));
    }

    [Fact]
    public async Task Objects_are_isolated_per_tenant()
    {
        var store = Store();
        await store.PutAsync(Object("acme", "secret", "acme-only"), CancellationToken.None);

        Assert.Null(await store.GetAsync("globex", "secret", CancellationToken.None));
        Assert.False(await store.ExistsAsync("globex", "secret", CancellationToken.None));
        Assert.Empty(await store.ListAsync("globex", "", CancellationToken.None));
    }

    [Fact]
    public async Task List_filters_by_prefix_and_orders_by_key()
    {
        var store = Store();
        await store.PutAsync(Object("acme", "reports/b.csv", "b"), CancellationToken.None);
        await store.PutAsync(Object("acme", "reports/a.csv", "a"), CancellationToken.None);
        await store.PutAsync(Object("acme", "exports/z.csv", "z"), CancellationToken.None);

        var refs = await store.ListAsync("acme", "reports/", CancellationToken.None);

        Assert.Equal(["reports/a.csv", "reports/b.csv"], refs.Select(r => r.Key));
        Assert.Equal(1, refs[0].Size);
    }

    [Fact]
    public async Task An_empty_prefix_lists_all_of_a_tenants_objects()
    {
        var store = Store();
        await store.PutAsync(Object("acme", "a", "1"), CancellationToken.None);
        await store.PutAsync(Object("acme", "b", "2"), CancellationToken.None);

        Assert.Equal(2, (await store.ListAsync("acme", "", CancellationToken.None)).Count);
    }

    [Fact]
    public async Task A_put_over_the_size_limit_is_rejected()
    {
        var store = Store(maxSize: 4);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            store.PutAsync(Object("acme", "big", "12345"), CancellationToken.None)); // 5 bytes > 4

        Assert.False(await store.ExistsAsync("acme", "big", CancellationToken.None));
    }

    [Fact]
    public async Task An_unlimited_store_accepts_any_size()
    {
        var store = Store(maxSize: 0);
        await store.PutAsync(Object("acme", "big", new string('x', 10_000)), CancellationToken.None);

        Assert.True(await store.ExistsAsync("acme", "big", CancellationToken.None));
    }
}
