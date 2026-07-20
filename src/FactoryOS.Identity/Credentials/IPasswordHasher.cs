namespace FactoryOS.Identity.Credentials;

/// <summary>Hashes and verifies user passwords.</summary>
public interface IPasswordHasher
{
    /// <summary>Hashes a plaintext password into a self-describing, storable string.</summary>
    /// <param name="password">The plaintext password.</param>
    /// <returns>The encoded hash.</returns>
    string Hash(string password);

    /// <summary>Verifies a plaintext password against an encoded hash in constant time.</summary>
    /// <param name="password">The plaintext password.</param>
    /// <param name="encodedHash">The encoded hash previously produced by <see cref="Hash"/>.</param>
    /// <returns><see langword="true"/> when the password matches.</returns>
    bool Verify(string password, string encodedHash);
}
