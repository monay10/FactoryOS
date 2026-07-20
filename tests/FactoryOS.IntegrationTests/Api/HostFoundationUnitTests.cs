using FactoryOS.Api.Hosting;
using Microsoft.Extensions.Options;

namespace FactoryOS.IntegrationTests.Api;

/// <summary>
/// Focused checks on the host-foundation building blocks that do not need a running server: version parsing and
/// reading, the OpenAPI document shape and the supported-culture resolution.
/// </summary>
public sealed class HostFoundationUnitTests
{
    [Theory]
    [InlineData("1", 1, 0)]
    [InlineData("2.5", 2, 5)]
    [InlineData(" 3.4 ", 3, 4)]
    public void ApiVersion_parses_major_and_minor(string text, int major, int minor)
    {
        var version = ApiVersion.Parse(text);

        Assert.Equal(major, version.Major);
        Assert.Equal(minor, version.Minor);
        Assert.Equal($"{major}.{minor}", version.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("x")]
    [InlineData("1.x")]
    public void ApiVersion_rejects_invalid_text(string text)
    {
        Assert.False(ApiVersion.TryParse(text, out _));
    }

    [Fact]
    public void The_version_reader_prefers_the_header_then_query_then_default()
    {
        var reader = new ApiVersionReader(Options.Create(new ApiVersioningSettings { DefaultVersion = "1.0" }));

        Assert.Equal(new ApiVersion(2, 0), reader.Read("2.0", "3.0"));
        Assert.Equal(new ApiVersion(3, 1), reader.Read(null, "3.1"));
        Assert.Equal(new ApiVersion(1, 0), reader.Read(null, null));
    }

    [Fact]
    public void The_openapi_document_carries_the_info_and_health_paths()
    {
        var document = OpenApiDocumentFactory.Build(
            new OpenApiSettings { Title = "T", Description = "D" },
            new ApiVersion(1, 2));

        Assert.Equal("3.0.1", document["openapi"]);
        var paths = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
            Convert(document["paths"]));
        Assert.True(paths.ContainsKey("/health"));
        Assert.True(paths.ContainsKey("/health/live"));
        Assert.True(paths.ContainsKey("/health/ready"));
    }

    [Fact]
    public void Supported_cultures_always_include_the_default_first_without_duplicates()
    {
        var settings = new LocalizationSettings { DefaultCulture = "en" };
        settings.SupportedCultures.Add("tr");
        settings.SupportedCultures.Add("en");

        var resolved = settings.ResolveSupportedCultures();

        Assert.Equal("en", resolved[0]);
        Assert.Equal(2, resolved.Count);
    }

    private static IReadOnlyDictionary<string, object?> Convert(object? value) =>
        (IReadOnlyDictionary<string, object?>)value!;
}
