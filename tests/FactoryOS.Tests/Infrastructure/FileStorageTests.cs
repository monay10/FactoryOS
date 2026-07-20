using System.Text;
using FactoryOS.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using InfraFileStorage = FactoryOS.Infrastructure.Files.FileStorage;

namespace FactoryOS.Tests.Infrastructure;

public sealed class FileStorageTests : IDisposable
{
    private readonly string _root;
    private readonly InfraFileStorage _storage;

    public FileStorageTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "factoryos-tests", Guid.NewGuid().ToString("N"));
        _storage = new InfraFileStorage(Options.Create(new InfrastructureOptions { FileStorageRootPath = _root }));
    }

    private static Stream Content(string text) => new MemoryStream(Encoding.UTF8.GetBytes(text));

    [Fact]
    public async Task A_saved_file_exists_and_reads_back()
    {
        await _storage.SaveAsync("reports/oee.txt", Content("hello"));

        Assert.True(await _storage.ExistsAsync("reports/oee.txt"));

        await using var stream = await _storage.OpenReadAsync("reports/oee.txt");
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);
        Assert.Equal("hello", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task A_missing_file_does_not_exist_and_opens_as_null()
    {
        Assert.False(await _storage.ExistsAsync("absent.txt"));
        Assert.Null(await _storage.OpenReadAsync("absent.txt"));
    }

    [Fact]
    public async Task A_deleted_file_is_gone()
    {
        await _storage.SaveAsync("a.txt", Content("x"));

        await _storage.DeleteAsync("a.txt");

        Assert.False(await _storage.ExistsAsync("a.txt"));
    }

    [Fact]
    public async Task Deleting_a_missing_file_is_a_no_op()
    {
        await _storage.DeleteAsync("never.txt");

        Assert.False(await _storage.ExistsAsync("never.txt"));
    }

    [Fact]
    public async Task A_path_that_escapes_the_root_is_rejected()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _storage.ExistsAsync(Path.Combine("..", "..", "escape.txt")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
