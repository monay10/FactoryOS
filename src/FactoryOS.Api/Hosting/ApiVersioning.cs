using System.Globalization;
using FactoryOS.Shared.Guards;
using Microsoft.Extensions.Options;

namespace FactoryOS.Api.Hosting;

/// <summary>An API version expressed as <c>major.minor</c>, comparable and parseable from configuration or requests.</summary>
/// <param name="Major">The major component.</param>
/// <param name="Minor">The minor component.</param>
public readonly record struct ApiVersion(int Major, int Minor)
{
    /// <summary>Parses an API version from its <c>major</c> or <c>major.minor</c> text form.</summary>
    /// <param name="value">The text to parse (for example <c>1</c> or <c>1.2</c>).</param>
    /// <returns>The parsed version.</returns>
    /// <exception cref="FormatException">Thrown when <paramref name="value"/> is not a valid version.</exception>
    public static ApiVersion Parse(string value)
    {
        Guard.AgainstNullOrWhiteSpace(value);

        var parts = value.Split('.', 2, StringSplitOptions.TrimEntries);
        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var major) || major < 0)
        {
            throw new FormatException($"'{value}' is not a valid API version.");
        }

        var minor = 0;
        if (parts.Length == 2
            && !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out minor))
        {
            throw new FormatException($"'{value}' is not a valid API version.");
        }

        return new ApiVersion(major, Math.Max(minor, 0));
    }

    /// <summary>Attempts to parse an API version, returning <see langword="false"/> instead of throwing on failure.</summary>
    /// <param name="value">The text to parse.</param>
    /// <param name="version">The parsed version when successful.</param>
    /// <returns><see langword="true"/> when parsing succeeded.</returns>
    public static bool TryParse(string? value, out ApiVersion version)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            try
            {
                version = Parse(value);
                return true;
            }
            catch (FormatException)
            {
                // Fall through to the failure result.
            }
        }

        version = default;
        return false;
    }

    /// <inheritdoc />
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Major}.{Minor}");
}

/// <summary>Reads the API version a request asks for, falling back to the configured default.</summary>
public interface IApiVersionReader
{
    /// <summary>Gets the default version applied when a request specifies none.</summary>
    ApiVersion DefaultVersion { get; }

    /// <summary>Reads the requested version from a header value and a query value, or the default when both are absent.</summary>
    /// <param name="headerValue">The value of the version header, if present.</param>
    /// <param name="queryValue">The value of the version query parameter, if present.</param>
    /// <returns>The resolved version.</returns>
    ApiVersion Read(string? headerValue, string? queryValue);
}

/// <summary>The default <see cref="IApiVersionReader"/>, honoring the header first, then the query, then the default.</summary>
public sealed class ApiVersionReader : IApiVersionReader
{
    /// <summary>Initializes a new instance of the <see cref="ApiVersionReader"/> class.</summary>
    /// <param name="options">The versioning settings carrying the default version.</param>
    public ApiVersionReader(IOptions<ApiVersioningSettings> options)
    {
        Guard.AgainstNull(options);
        DefaultVersion = ApiVersion.Parse(options.Value.DefaultVersion);
    }

    /// <inheritdoc />
    public ApiVersion DefaultVersion { get; }

    /// <inheritdoc />
    public ApiVersion Read(string? headerValue, string? queryValue)
    {
        if (ApiVersion.TryParse(headerValue, out var fromHeader))
        {
            return fromHeader;
        }

        return ApiVersion.TryParse(queryValue, out var fromQuery) ? fromQuery : DefaultVersion;
    }
}
