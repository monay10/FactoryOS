using System.Globalization;

namespace FactoryOS.Shared.ValueObjects;

/// <summary>
/// A geographic point as latitude and longitude in decimal degrees (WGS 84). Immutable with value equality.
/// Latitude is constrained to <c>[-90, 90]</c> and longitude to <c>[-180, 180]</c>.
/// </summary>
public sealed record GeoLocation
{
    private GeoLocation(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    /// <summary>Gets the latitude in decimal degrees.</summary>
    public double Latitude { get; }

    /// <summary>Gets the longitude in decimal degrees.</summary>
    public double Longitude { get; }

    /// <summary>Creates a geographic location, validating the coordinate ranges.</summary>
    /// <param name="latitude">The latitude in <c>[-90, 90]</c>.</param>
    /// <param name="longitude">The longitude in <c>[-180, 180]</c>.</param>
    /// <returns>A new <see cref="GeoLocation"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a coordinate is out of range.</exception>
    public static GeoLocation Create(double latitude, double longitude)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(latitude, -90d);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(latitude, 90d);
        ArgumentOutOfRangeException.ThrowIfLessThan(longitude, -180d);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(longitude, 180d);
        return new GeoLocation(latitude, longitude);
    }

    /// <inheritdoc />
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Latitude:0.######}, {Longitude:0.######}");
}
