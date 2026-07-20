using FactoryOS.Contracts.Plugins;
using FactoryOS.Contracts.Storage;
using FactoryOS.Plugins.FileStorage.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FactoryOS.Plugins.FileStorage;

/// <summary>
/// The File Storage plugin — the Platform-layer object-store door. It contributes an <see cref="IObjectStore"/>
/// (in-memory by default) that any module uses to put and get blobs — reports, attachments, exports — without
/// knowing the backing store. It is a provided capability, like the event bus: no module talks to MinIO/S3
/// directly. Removing this folder removes blob storage with zero core changes; swapping the implementation
/// (MinIO/S3) touches no caller.
/// </summary>
public sealed class FileStoragePlugin : PluginBase
{
    /// <summary>The plugin key, matching <c>module.json</c>.</summary>
    public const string PluginKey = "filestorage";

    /// <inheritdoc />
    public override string Key => PluginKey;

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(new FileStorageOptions());
        services.TryAddSingleton<IObjectStore>(static sp => new InMemoryObjectStore(sp.GetRequiredService<FileStorageOptions>()));
    }
}
