using FactoryOS.Application.Files;
using FactoryOS.Infrastructure.Configuration;
using FactoryOS.Shared.Guards;
using Microsoft.Extensions.Options;

namespace FactoryOS.Infrastructure.Files;

/// <summary>
/// A read-only <see cref="IFileProvider"/> over the local file system, rooted at the configured storage path. Every
/// path is resolved beneath the root and rejected if it would escape it, preventing directory-traversal reads.
/// </summary>
public class LocalFileProvider : IFileProvider
{
    private readonly string _root;

    /// <summary>Initializes a new instance of the <see cref="LocalFileProvider"/> class.</summary>
    /// <param name="options">The infrastructure options carrying the storage root path.</param>
    public LocalFileProvider(IOptions<InfrastructureOptions> options)
    {
        Guard.AgainstNull(options);
        _root = Path.GetFullPath(options.Value.FileStorageRootPath);
    }

    /// <summary>Gets the absolute root path beneath which all files are stored.</summary>
    protected string Root => _root;

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(File.Exists(Resolve(path)));
    }

    /// <inheritdoc />
    public Task<Stream?> OpenReadAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var full = Resolve(path);
        Stream? stream = File.Exists(full)
            ? new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read)
            : null;
        return Task.FromResult(stream);
    }

    /// <summary>Resolves a caller-supplied path to an absolute path guaranteed to live beneath the storage root.</summary>
    /// <param name="path">The caller-supplied relative path or object key.</param>
    /// <returns>The absolute, validated file-system path.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the path escapes the storage root.</exception>
    protected string Resolve(string path)
    {
        Guard.AgainstNullOrWhiteSpace(path);

        var full = Path.GetFullPath(Path.Combine(_root, path));
        var boundary = _root.EndsWith(Path.DirectorySeparatorChar) ? _root : _root + Path.DirectorySeparatorChar;
        if (!full.StartsWith(boundary, StringComparison.Ordinal) && full != _root)
        {
            throw new UnauthorizedAccessException($"The path '{path}' escapes the storage root.");
        }

        return full;
    }
}

/// <summary>
/// The default <see cref="IFileStorage"/>: read and write access over the local file system, extending
/// <see cref="LocalFileProvider"/> with save and delete. Writes create any missing directories beneath the root.
/// </summary>
public sealed class FileStorage : LocalFileProvider, IFileStorage
{
    /// <summary>Initializes a new instance of the <see cref="FileStorage"/> class.</summary>
    /// <param name="options">The infrastructure options carrying the storage root path.</param>
    public FileStorage(IOptions<InfrastructureOptions> options)
        : base(options)
    {
    }

    /// <inheritdoc />
    public async Task SaveAsync(string path, Stream content, CancellationToken cancellationToken = default)
    {
        Guard.AgainstNull(content);

        var full = Resolve(path);
        var directory = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var target = new FileStream(full, FileMode.Create, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(target, cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var full = Resolve(path);
        if (File.Exists(full))
        {
            File.Delete(full);
        }

        return Task.CompletedTask;
    }
}
