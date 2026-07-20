using System.Globalization;
using System.Security.Cryptography;

namespace FactoryOS.Identity.Credentials;

/// <summary>
/// A PBKDF2 (SHA-256) <see cref="IPasswordHasher"/>. The encoded format is
/// <c>{iterations}.{saltBase64}.{hashBase64}</c>, so the parameters travel with the hash and can
/// evolve without breaking existing credentials. Verification is constant-time.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    /// <inheritdoc />
    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, KeySize);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}");
    }

    /// <inheritdoc />
    public bool Verify(string password, string encodedHash)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        ArgumentException.ThrowIfNullOrEmpty(encodedHash);

        var parts = encodedHash.Split('.');
        if (parts.Length != 3
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations))
        {
            return false;
        }

        byte[] salt;
        byte[] expectedKey;
        try
        {
            salt = Convert.FromBase64String(parts[1]);
            expectedKey = Convert.FromBase64String(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actualKey = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, expectedKey.Length);
        return CryptographicOperations.FixedTimeEquals(actualKey, expectedKey);
    }
}
