using FactoryOS.Domain.Results;
using FactoryOS.Plugins.Runtime.Configuration;
using FactoryOS.Plugins.Runtime.Domain;

namespace FactoryOS.Plugins.Runtime.Discovery;

/// <summary>
/// Reads one package folder into a <see cref="PluginPackage"/>.
/// <para>
/// A <c>module.sig</c> beside the manifest takes precedence over a signature written inside it, because a
/// signature that lives in the file it vouches for can be rewritten by whoever rewrites the file.
/// </para>
/// </summary>
public sealed class PluginPackageReader
{
    /// <summary>Reads the package in a folder.</summary>
    /// <param name="directory">The folder holding <c>module.json</c>.</param>
    /// <returns>A successful result with the package, or a failure describing the problem.</returns>
    public Result<PluginPackage> Read(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        var manifestPath = Path.Combine(directory, PluginRuntimeConstants.ManifestFileName);
        var manifest = PluginRuntimeManifestReader.ReadFile(manifestPath);
        if (manifest.IsFailure)
        {
            return Result.Failure<PluginPackage>(manifest.Error);
        }

        var detached = ReadDetachedSignature(directory);
        if (detached.IsFailure)
        {
            return Result.Failure<PluginPackage>(detached.Error);
        }

        var signature = detached.Value.IsPresent ? detached.Value : manifest.Value.Signature;
        return new PluginPackage(manifest.Value.Manifest, manifest.Value.Definition, signature);
    }

    /// <summary>
    /// Reads the detached signature beside a manifest, if there is one. The file holds three
    /// whitespace-separated tokens: algorithm, key identifier and the Base64 value.
    /// </summary>
    /// <param name="directory">The package folder.</param>
    /// <returns>The signature, <see cref="PluginSignature.None"/> when absent, or a failure when malformed.</returns>
    private static Result<PluginSignature> ReadDetachedSignature(string directory)
    {
        var path = Path.Combine(directory, PluginRuntimeConstants.SignatureFileName);
        if (!File.Exists(path))
        {
            return PluginSignature.None;
        }

        var tokens = File.ReadAllText(path)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (tokens.Length != 3)
        {
            return Result.Failure<PluginSignature>(Error.Validation(
                "Plugin.Runtime.Signature.Malformed",
                $"The signature file '{path}' must hold three tokens: algorithm, key identifier and value."));
        }

        if (!Enum.TryParse<PluginSignatureAlgorithm>(tokens[0], ignoreCase: true, out var algorithm)
            || algorithm == PluginSignatureAlgorithm.None)
        {
            return Result.Failure<PluginSignature>(Error.Validation(
                "Plugin.Runtime.Signature.Malformed",
                $"'{tokens[0]}' is not a supported signature algorithm."));
        }

        return new PluginSignature(algorithm, tokens[2]) { KeyId = tokens[1] };
    }
}

/// <summary>Why one folder under the discovery root did not yield a package.</summary>
/// <param name="Location">The folder.</param>
/// <param name="Reason">Why it was rejected, in a form someone can act on.</param>
public sealed record PluginDiscoveryRejection(string Location, string Reason);

/// <summary>
/// What a discovery sweep found: the packages that read cleanly, and the folders that did not — with the
/// reason. A package that silently fails to appear is a support call; one that appears with its problem
/// stated is a fix.
/// </summary>
/// <param name="Packages">The packages that read cleanly.</param>
/// <param name="Rejected">The folders that did not, and why.</param>
public sealed record PluginDiscoveryResult(
    IReadOnlyList<PluginPackage> Packages, IReadOnlyList<PluginDiscoveryRejection> Rejected)
{
    /// <summary>Gets the empty result.</summary>
    public static PluginDiscoveryResult Empty { get; } = new([], []);

    /// <summary>Gets the number of packages found.</summary>
    public int Count => Packages.Count;

    /// <summary>Finds a discovered package by key.</summary>
    /// <param name="key">The plugin key.</param>
    /// <returns>The package, or <see langword="null"/> when it was not found.</returns>
    public PluginPackage? Find(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return Packages.FirstOrDefault(package => string.Equals(package.Key, key, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>Finds plugin packages on disk.</summary>
public interface IPluginPackageDiscovery
{
    /// <summary>Scans the immediate subfolders of a root for plugin packages.</summary>
    /// <param name="rootDirectory">The folder whose child folders each hold one package.</param>
    /// <returns>What was found, and what was rejected.</returns>
    PluginDiscoveryResult Discover(string rootDirectory);
}

/// <summary>
/// Default <see cref="IPluginPackageDiscovery"/>: each immediate subfolder of the root holding a
/// <c>module.json</c> is one package — the same convention the plugin framework and the connector runtime
/// already use, deliberately, because a factory engineer should learn the layout once.
/// </summary>
public sealed class PluginRuntimeDiscovery : IPluginPackageDiscovery
{
    private readonly PluginPackageReader _reader;

    /// <summary>Initializes a new instance of the <see cref="PluginRuntimeDiscovery"/> class.</summary>
    /// <param name="reader">The package reader.</param>
    public PluginRuntimeDiscovery(PluginPackageReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _reader = reader;
    }

    /// <inheritdoc />
    public PluginDiscoveryResult Discover(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        if (!Directory.Exists(rootDirectory))
        {
            return PluginDiscoveryResult.Empty;
        }

        var packages = new List<PluginPackage>();
        var rejected = new List<PluginDiscoveryRejection>();

        foreach (var directory in Directory.EnumerateDirectories(rootDirectory)
            .OrderBy(path => path, StringComparer.Ordinal))
        {
            if (!File.Exists(Path.Combine(directory, PluginRuntimeConstants.ManifestFileName)))
            {
                continue;
            }

            var package = _reader.Read(directory);
            if (package.IsSuccess)
            {
                packages.Add(package.Value);
            }
            else
            {
                rejected.Add(new PluginDiscoveryRejection(directory, package.Error.Description));
            }
        }

        return new PluginDiscoveryResult(packages, rejected);
    }
}
