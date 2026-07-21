using System.Security.Cryptography;
using System.Text;
using FactoryOS.Connectors.Framework.Configuration;

namespace FactoryOS.Connectors.Framework.Security;

/// <summary>
/// Protects and unprotects connector secret settings. A protected value is prefixed with
/// <see cref="ConnectorConstants.SecretPrefix"/>; anything without the prefix is plaintext.
/// </summary>
public interface IConnectorSecretProtector
{
    /// <summary>Determines whether a value is a protected secret.</summary>
    /// <param name="value">The value to inspect.</param>
    /// <returns><see langword="true"/> when the value carries the secret prefix.</returns>
    bool IsProtected(string value);

    /// <summary>Encrypts a plaintext value into a protected, prefixed secret.</summary>
    /// <param name="plaintext">The plaintext to protect.</param>
    /// <returns>The protected value.</returns>
    string Protect(string plaintext);

    /// <summary>Decrypts a protected value, returning plaintext unchanged.</summary>
    /// <param name="value">The value to unprotect.</param>
    /// <returns>The plaintext.</returns>
    string Unprotect(string value);
}

/// <summary>
/// A no-op <see cref="IConnectorSecretProtector"/> used when no encryption key is configured (development):
/// nothing is treated as protected and values pass through unchanged.
/// </summary>
public sealed class PassthroughConnectorSecretProtector : IConnectorSecretProtector
{
    /// <inheritdoc />
    public bool IsProtected(string value) => false;

    /// <inheritdoc />
    public string Protect(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        return plaintext;
    }

    /// <inheritdoc />
    public string Unprotect(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value;
    }
}

/// <summary>
/// An AES-GCM <see cref="IConnectorSecretProtector"/>. The protected form is
/// <c>enc:base64(nonce | tag | ciphertext)</c>. The key is supplied out-of-band (never committed) and must be
/// 128, 192 or 256 bits.
/// </summary>
public sealed class AesGcmConnectorSecretProtector : IConnectorSecretProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;

    /// <summary>Initializes a new instance of the <see cref="AesGcmConnectorSecretProtector"/> class.</summary>
    /// <param name="key">The AES key (16, 24 or 32 bytes).</param>
    /// <exception cref="ArgumentException">Thrown when the key length is invalid.</exception>
    public AesGcmConnectorSecretProtector(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length is not (16 or 24 or 32))
        {
            throw new ArgumentException("The AES key must be 16, 24 or 32 bytes.", nameof(key));
        }

        _key = (byte[])key.Clone();
    }

    /// <summary>Creates a protector from a base64-encoded key.</summary>
    /// <param name="base64Key">The base64-encoded AES key.</param>
    /// <returns>The protector.</returns>
    public static AesGcmConnectorSecretProtector FromBase64Key(string base64Key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(base64Key);
        return new AesGcmConnectorSecretProtector(Convert.FromBase64String(base64Key));
    }

    /// <inheritdoc />
    public bool IsProtected(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.StartsWith(ConnectorConstants.SecretPrefix, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public string Protect(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];

        using (var aes = new AesGcm(_key, TagSize))
        {
            aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);
        }

        var payload = new byte[NonceSize + TagSize + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, payload, NonceSize, TagSize);
        Buffer.BlockCopy(ciphertext, 0, payload, NonceSize + TagSize, ciphertext.Length);

        return ConnectorConstants.SecretPrefix + Convert.ToBase64String(payload);
    }

    /// <inheritdoc />
    public string Unprotect(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (!IsProtected(value))
        {
            return value;
        }

        var payload = Convert.FromBase64String(value[ConnectorConstants.SecretPrefix.Length..]);
        if (payload.Length < NonceSize + TagSize)
        {
            throw new FormatException("The protected connector secret is malformed.");
        }

        var nonce = payload[..NonceSize];
        var tag = payload[NonceSize..(NonceSize + TagSize)];
        var ciphertext = payload[(NonceSize + TagSize)..];
        var plaintext = new byte[ciphertext.Length];

        using (var aes = new AesGcm(_key, TagSize))
        {
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
        }

        return Encoding.UTF8.GetString(plaintext);
    }
}
