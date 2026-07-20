using System.Text;
using FactoryOS.Contracts.Storage;
using FactoryOS.Plugins.FileStorage;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// The File Storage plugin proven through the host container: installing it contributes a working
/// <see cref="IObjectStore"/> capability that round-trips objects and keeps tenants isolated — a provided
/// Platform service, resolved like any other, that a module would consume without a direct storage dependency.
/// </summary>
public sealed class FileStoragePluginTests
{
    [Fact]
    public async Task The_plugin_provides_a_working_tenant_isolated_object_store()
    {
        var services = new ServiceCollection();
        new FileStoragePlugin().ConfigureServices(services);

        var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IObjectStore>();

        await store.PutAsync(
            new StoredObject
            {
                Tenant = "acme",
                Key = "reports/2026-07.pdf",
                ContentType = "application/pdf",
                Content = Encoding.UTF8.GetBytes("%PDF-1.7 report"),
            },
            CancellationToken.None);

        var got = await store.GetAsync("acme", "reports/2026-07.pdf", CancellationToken.None);
        Assert.NotNull(got);
        Assert.Equal("application/pdf", got.ContentType);
        Assert.StartsWith("%PDF", Encoding.UTF8.GetString(got.Content.Span), StringComparison.Ordinal);

        // Another tenant sees nothing.
        Assert.Null(await store.GetAsync("globex", "reports/2026-07.pdf", CancellationToken.None));

        var listed = await store.ListAsync("acme", "reports/", CancellationToken.None);
        Assert.Equal("reports/2026-07.pdf", Assert.Single(listed).Key);
    }
}
