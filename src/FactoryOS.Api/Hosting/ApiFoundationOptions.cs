namespace FactoryOS.Api.Hosting;

/// <summary>Well-known configuration section names bound by the API host foundation.</summary>
public static class ApiHostSections
{
    /// <summary>The CORS configuration section.</summary>
    public const string Cors = "Cors";

    /// <summary>The request-localization configuration section.</summary>
    public const string Localization = "Localization";

    /// <summary>The API-versioning configuration section.</summary>
    public const string ApiVersioning = "ApiVersioning";

    /// <summary>The OpenAPI/Swagger configuration section.</summary>
    public const string OpenApi = "OpenApi";
}

/// <summary>Options for the named CORS policy the host exposes.</summary>
public sealed class CorsSettings
{
    /// <summary>Gets or sets the name of the CORS policy applied to the pipeline.</summary>
    public string PolicyName { get; set; } = "FactoryOSDefaultCors";

    /// <summary>Gets or sets a value indicating whether any origin is allowed. Prefer an explicit allow-list.</summary>
    public bool AllowAnyOrigin { get; set; }

    /// <summary>Gets the explicit allow-list of origins. When empty and <see cref="AllowAnyOrigin"/> is false, no
    /// cross-origin caller is permitted.</summary>
    public IList<string> AllowedOrigins { get; } = new List<string>();
}

/// <summary>Options for request localization: the supported cultures and the default.</summary>
public sealed class LocalizationSettings
{
    /// <summary>The culture applied when the fallback default is otherwise unspecified.</summary>
    public const string FallbackDefaultCulture = "en";

    /// <summary>Gets or sets the default culture used when a request resolves none.</summary>
    public string DefaultCulture { get; set; } = FallbackDefaultCulture;

    /// <summary>Gets the cultures the host accepts. When empty, only the <see cref="DefaultCulture"/> is supported.</summary>
    public IList<string> SupportedCultures { get; } = new List<string>();

    /// <summary>Builds the effective, de-duplicated set of supported cultures, always including the default.</summary>
    /// <returns>The supported culture names, with the default guaranteed present and first.</returns>
    public IReadOnlyList<string> ResolveSupportedCultures()
    {
        var resolved = new List<string> { DefaultCulture };
        foreach (var culture in SupportedCultures)
        {
            if (!string.IsNullOrWhiteSpace(culture)
                && !resolved.Contains(culture, StringComparer.OrdinalIgnoreCase))
            {
                resolved.Add(culture);
            }
        }

        return resolved;
    }
}

/// <summary>Options for the API-versioning foundation.</summary>
public sealed class ApiVersioningSettings
{
    /// <summary>The version assumed when a request specifies none.</summary>
    public const string FallbackDefaultVersion = "1.0";

    /// <summary>Gets or sets the default API version, in <c>major.minor</c> form.</summary>
    public string DefaultVersion { get; set; } = FallbackDefaultVersion;

    /// <summary>Gets or sets the header a caller may use to request a specific version.</summary>
    public string HeaderName { get; set; } = "X-Api-Version";

    /// <summary>Gets or sets the query-string parameter a caller may use to request a specific version.</summary>
    public string QueryParameterName { get; set; } = "api-version";
}

/// <summary>Options describing the OpenAPI document and Swagger UI the host serves.</summary>
public sealed class OpenApiSettings
{
    /// <summary>Gets or sets a value indicating whether the OpenAPI document and Swagger UI are exposed.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the document title.</summary>
    public string Title { get; set; } = "FactoryOS API";

    /// <summary>Gets or sets the document description.</summary>
    public string Description { get; set; } =
        "The FactoryOS Enterprise host API. Business capabilities are served by plugin modules under /m/<key>/.";
}
