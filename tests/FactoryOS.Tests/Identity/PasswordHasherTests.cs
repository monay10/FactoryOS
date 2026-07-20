using FactoryOS.Identity.Credentials;

namespace FactoryOS.Tests.Identity;

public sealed class PasswordHasherTests
{
    private static readonly Pbkdf2PasswordHasher Hasher = new();

    [Fact]
    public void Hash_then_verify_succeeds()
    {
        var hash = Hasher.Hash("Sup3r$ecret");

        Assert.True(Hasher.Verify("Sup3r$ecret", hash));
    }

    [Fact]
    public void Verify_rejects_the_wrong_password()
    {
        var hash = Hasher.Hash("Sup3r$ecret");

        Assert.False(Hasher.Verify("wrong", hash));
    }

    [Fact]
    public void Hashes_are_salted_and_therefore_unique()
    {
        Assert.NotEqual(Hasher.Hash("same"), Hasher.Hash("same"));
    }

    [Fact]
    public void Verify_rejects_a_malformed_hash()
    {
        Assert.False(Hasher.Verify("password", "not-a-valid-hash"));
    }
}
