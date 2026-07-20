using System.Security.Claims;

namespace FactoryOS.Identity.Claims;

/// <summary>
/// A transport-friendly claim descriptor (type and value) that the identity foundation resolves for a
/// user, independent of the <see cref="Claim"/> runtime type. Converts to and from <see cref="Claim"/> so
/// callers can carry claims across boundaries without depending on <c>System.Security.Claims</c>.
/// </summary>
/// <param name="Type">The claim type (e.g. one of <see cref="FactoryClaimTypes"/>).</param>
/// <param name="Value">The claim value.</param>
public sealed record ApplicationClaim(string Type, string Value)
{
    /// <summary>Materializes this descriptor into a runtime <see cref="Claim"/>.</summary>
    /// <returns>The equivalent <see cref="Claim"/>.</returns>
    public Claim ToClaim() => new(Type, Value);

    /// <summary>Creates a descriptor from a runtime <see cref="Claim"/>.</summary>
    /// <param name="claim">The claim to describe.</param>
    /// <returns>The equivalent <see cref="ApplicationClaim"/>.</returns>
    public static ApplicationClaim FromClaim(Claim claim)
    {
        ArgumentNullException.ThrowIfNull(claim);
        return new ApplicationClaim(claim.Type, claim.Value);
    }
}
