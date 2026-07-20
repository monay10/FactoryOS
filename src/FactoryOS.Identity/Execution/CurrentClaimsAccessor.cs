using System.Security.Claims;
using FactoryOS.Identity.Context;

namespace FactoryOS.Identity.Execution;

/// <summary>Reads the current principal's claims for the current scope.</summary>
public interface ICurrentClaimsAccessor
{
    /// <summary>Gets every claim carried by the current principal.</summary>
    IReadOnlyList<Claim> Claims { get; }

    /// <summary>Finds the first value of a claim type.</summary>
    /// <param name="type">The claim type.</param>
    /// <returns>The first matching value, or <see langword="null"/> when absent.</returns>
    string? Find(string type);

    /// <summary>Finds every value of a claim type.</summary>
    /// <param name="type">The claim type.</param>
    /// <returns>All matching values, in claim order.</returns>
    IReadOnlyList<string> FindAll(string type);

    /// <summary>Determines whether the principal carries a claim with the given type and value.</summary>
    /// <param name="type">The claim type.</param>
    /// <param name="value">The claim value.</param>
    /// <returns><see langword="true"/> when the claim is present.</returns>
    bool Has(string type, string value);
}

/// <summary>Default <see cref="ICurrentClaimsAccessor"/> reading from the scoped <see cref="IdentityContext"/>.</summary>
public sealed class CurrentClaimsAccessor : ICurrentClaimsAccessor
{
    private readonly IdentityContext _context;

    /// <summary>Initializes a new instance of the <see cref="CurrentClaimsAccessor"/> class.</summary>
    /// <param name="context">The scoped identity context.</param>
    public CurrentClaimsAccessor(IdentityContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public IReadOnlyList<Claim> Claims => _context.Principal.Claims.ToArray();

    /// <inheritdoc />
    public string? Find(string type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        return _context.Principal.FindFirst(type)?.Value;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> FindAll(string type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        return _context.Principal.FindAll(type).Select(claim => claim.Value).ToArray();
    }

    /// <inheritdoc />
    public bool Has(string type, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentNullException.ThrowIfNull(value);
        return _context.Principal.HasClaim(type, value);
    }
}
