using FactoryOS.Shared.Guards;

namespace FactoryOS.Shared.ValueObjects;

/// <summary>
/// A postal address. Immutable with value equality. The first line, city, postal code and country are required;
/// the second line and region/state are optional.
/// </summary>
public sealed record Address
{
    private Address(string line1, string? line2, string city, string? region, string postalCode, string country)
    {
        Line1 = line1;
        Line2 = line2;
        City = city;
        Region = region;
        PostalCode = postalCode;
        Country = country;
    }

    /// <summary>Gets the first address line (street and number).</summary>
    public string Line1 { get; }

    /// <summary>Gets the optional second address line (suite, unit).</summary>
    public string? Line2 { get; }

    /// <summary>Gets the city or town.</summary>
    public string City { get; }

    /// <summary>Gets the optional region, state or province.</summary>
    public string? Region { get; }

    /// <summary>Gets the postal or ZIP code.</summary>
    public string PostalCode { get; }

    /// <summary>Gets the country.</summary>
    public string Country { get; }

    /// <summary>Creates a postal address.</summary>
    /// <param name="line1">The first address line (required).</param>
    /// <param name="city">The city or town (required).</param>
    /// <param name="postalCode">The postal or ZIP code (required).</param>
    /// <param name="country">The country (required).</param>
    /// <param name="line2">The optional second address line.</param>
    /// <param name="region">The optional region, state or province.</param>
    /// <returns>A new <see cref="Address"/>.</returns>
    public static Address Create(
        string line1,
        string city,
        string postalCode,
        string country,
        string? line2 = null,
        string? region = null)
    {
        Guard.AgainstNullOrWhiteSpace(line1);
        Guard.AgainstNullOrWhiteSpace(city);
        Guard.AgainstNullOrWhiteSpace(postalCode);
        Guard.AgainstNullOrWhiteSpace(country);
        return new Address(line1.Trim(), line2?.Trim(), city.Trim(), region?.Trim(), postalCode.Trim(), country.Trim());
    }
}
