namespace FactoryOS.Application.Files;

/// <summary>Read access to stored files (for example objects in MinIO / S3).</summary>
public interface IFileProvider
{
    /// <summary>Determines whether a file exists.</summary>
    /// <param name="path">The file path or object key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> when the file exists.</returns>
    Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Opens a file for reading.</summary>
    /// <param name="path">The file path or object key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A readable stream, or <see langword="null"/> when the file does not exist.</returns>
    Task<Stream?> OpenReadAsync(string path, CancellationToken cancellationToken = default);
}

/// <summary>Read and write access to stored files.</summary>
public interface IFileStorage : IFileProvider
{
    /// <summary>Saves a file, overwriting any existing content at the path.</summary>
    /// <param name="path">The file path or object key.</param>
    /// <param name="content">The content to store.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the content has been stored.</returns>
    Task SaveAsync(string path, Stream content, CancellationToken cancellationToken = default);

    /// <summary>Deletes a file if it exists.</summary>
    /// <param name="path">The file path or object key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the file has been deleted.</returns>
    Task DeleteAsync(string path, CancellationToken cancellationToken = default);
}
