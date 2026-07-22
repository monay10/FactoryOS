using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using FactoryOS.Domain.Results;
using FactoryOS.Plugins.Runtime.Configuration;
using FactoryOS.Plugins.Runtime.Domain;
using Microsoft.Extensions.Options;

namespace FactoryOS.Plugins.Runtime.Security;

/// <summary>
/// Where the runtime gets the keys it verifies package signatures with.
/// <para>
/// This is a <b>port</b>. Signing keys belong to the identity and secret-management layers, not to the
/// plugin runtime; the default reads them from the environment so a development host works, and a real
/// deployment substitutes a vault. No key is ever written into a manifest, a package or a sample config.
/// </para>
/// </summary>
public interface IPluginSigningKeySource
{
    /// <summary>Resolves the key a signature names.</summary>
    /// <param name="keyId">The key identifier the signature carries.</param>
    /// <returns>The key material, or <see langword="null"/> when this source does not have it.</returns>
    byte[]? Find(string keyId);
}

/// <summary>
/// Reads signing keys from environment variables named <c>FACTORYOS_PLUGIN_KEY_&lt;keyId&gt;</c>, holding the
/// key Base64-encoded.
/// </summary>
public sealed class EnvironmentPluginSigningKeySource : IPluginSigningKeySource
{
    /// <summary>The prefix of the environment variable a key is read from.</summary>
    public const string VariablePrefix = "FACTORYOS_PLUGIN_KEY_";

    /// <inheritdoc />
    public byte[]? Find(string keyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);

        var value = Environment.GetEnvironmentVariable(VariablePrefix + keyId.ToUpperInvariant());
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        // A key is normally Base64; a development host may set a plain passphrase instead.
        var buffer = new byte[value.Length];
        return Convert.TryFromBase64String(value, buffer, out var written)
            ? buffer[..written]
            : Encoding.UTF8.GetBytes(value);
    }
}

/// <summary>An in-memory <see cref="IPluginSigningKeySource"/>, for a host that holds its keys itself.</summary>
public sealed class InMemoryPluginSigningKeySource : IPluginSigningKeySource
{
    private readonly ConcurrentDictionary<string, byte[]> _keys = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Adds or replaces a key.</summary>
    /// <param name="keyId">The key identifier.</param>
    /// <param name="key">The key material.</param>
    public void Add(string keyId, byte[] key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);
        ArgumentNullException.ThrowIfNull(key);
        _keys[keyId] = key;
    }

    /// <inheritdoc />
    public byte[]? Find(string keyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);
        return _keys.TryGetValue(keyId, out var key) ? key : null;
    }
}

/// <summary>
/// Verifies that a package is the one the signer vouched for.
/// <para>
/// Two rules make this worth having. An <b>invalid</b> signature is always fatal, whatever the configuration
/// says — a package claiming to be signed and failing to prove it is a worse signal than an unsigned one. An
/// <b>absent</b> signature is fatal only when the host requires signing, so first-party packages compiled
/// into the monolith still install while a Store package never does unsigned.
/// </para>
/// </summary>
public sealed class PluginSignatureValidator
{
    private readonly IReadOnlyList<IPluginSigningKeySource> _keys;
    private readonly PluginRuntimeOptions _options;

    /// <summary>Initializes a new instance of the <see cref="PluginSignatureValidator"/> class.</summary>
    /// <param name="keys">The key sources.</param>
    /// <param name="options">The runtime options.</param>
    public PluginSignatureValidator(IEnumerable<IPluginSigningKeySource> keys, IOptions<PluginRuntimeOptions> options)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(options);

        _keys = [.. keys];
        _options = options.Value;
    }

    /// <summary>Signs a package's canonical content, so a host can produce the signature it will verify.</summary>
    /// <param name="package">The package to sign.</param>
    /// <param name="key">The key material.</param>
    /// <returns>The Base64 signature value.</returns>
    public static string Sign(PluginPackage package, byte[] key)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(key);

        var content = Encoding.UTF8.GetBytes(package.CanonicalContent());
        return Convert.ToBase64String(HMACSHA256.HashData(key, content));
    }

    /// <summary>Verifies a package's signature.</summary>
    /// <param name="package">The package.</param>
    /// <returns>A successful result, or a failure explaining what could not be proved.</returns>
    public Result Validate(PluginPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        if (!package.Signature.IsPresent)
        {
            return _options.RequireSignature
                ? Result.Failure(Error.Validation(
                    "Plugin.Runtime.Signature.Missing",
                    $"Package '{package.Identity}' is unsigned and this host requires every package to be signed."))
                : Result.Success();
        }

        if (package.Signature.Algorithm != PluginSignatureAlgorithm.HmacSha256)
        {
            return Result.Failure(Error.Validation(
                "Plugin.Runtime.Signature.Unsupported",
                $"Package '{package.Identity}' is signed with {package.Signature.Algorithm}, which this host "
                + "cannot verify."));
        }

        if (string.IsNullOrWhiteSpace(package.Signature.KeyId))
        {
            return Result.Failure(Error.Validation(
                "Plugin.Runtime.Signature.NoKeyId",
                $"Package '{package.Identity}' carries a signature that does not name the key that produced it."));
        }

        var key = Resolve(package.Signature.KeyId);
        if (key is null)
        {
            return Result.Failure(Error.NotFound(
                "Plugin.Runtime.Signature.UnknownKey",
                $"Package '{package.Identity}' is signed with key '{package.Signature.KeyId}', which this host "
                + "does not trust."));
        }

        byte[] provided;
        try
        {
            provided = Convert.FromBase64String(package.Signature.Value);
        }
        catch (FormatException)
        {
            return Result.Failure(Error.Validation(
                "Plugin.Runtime.Signature.Malformed",
                $"Package '{package.Identity}' carries a signature that is not valid Base64."));
        }

        var expected = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(package.CanonicalContent()));

        // A fixed-time comparison: how long a rejection takes must not reveal how close a forgery was.
        return CryptographicOperations.FixedTimeEquals(expected, provided)
            ? Result.Success()
            : Result.Failure(Error.Validation(
                "Plugin.Runtime.Signature.Invalid",
                $"Package '{package.Identity}' does not match the signature it carries; its manifest has been "
                + "changed since it was signed."));
    }

    private byte[]? Resolve(string keyId)
    {
        foreach (var source in _keys)
        {
            var key = source.Find(keyId);
            if (key is not null)
            {
                return key;
            }
        }

        return null;
    }
}
