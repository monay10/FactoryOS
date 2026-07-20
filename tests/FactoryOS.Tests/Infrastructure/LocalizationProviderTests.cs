using FactoryOS.Infrastructure.Configuration;
using FactoryOS.Infrastructure.Localization;
using FactoryOS.Shared.Identifiers;
using Microsoft.Extensions.Options;

namespace FactoryOS.Tests.Infrastructure;

public sealed class LocalizationProviderTests
{
    private static LocalizationProvider Create(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? catalog = null,
        string defaultCulture = "en")
    {
        catalog ??= new Dictionary<string, IReadOnlyDictionary<string, string>>();
        var options = Options.Create(new InfrastructureOptions { DefaultCulture = defaultCulture });
        return new LocalizationProvider(catalog, options);
    }

    [Fact]
    public void A_missing_key_falls_back_to_the_key_itself()
    {
        var provider = Create();

        Assert.Equal("common.not_found", provider.Localize(new LocalizationKey("common.not_found")));
    }

    [Fact]
    public void A_translation_is_formatted_with_the_arguments()
    {
        var catalog = new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["en"] = new Dictionary<string, string> { ["greeting"] = "Hello {0}, you have {1} alerts" },
        };
        var provider = Create(catalog);

        var text = provider.Localize(new LocalizationKey("greeting"), "en", "Ada", 3);

        Assert.Equal("Hello Ada, you have 3 alerts", text);
    }

    [Fact]
    public void A_culture_without_the_key_falls_back_to_the_default_culture()
    {
        var catalog = new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["en"] = new Dictionary<string, string> { ["title"] = "Dashboard" },
        };
        var provider = Create(catalog, defaultCulture: "en");

        Assert.Equal("Dashboard", provider.Localize(new LocalizationKey("title"), "tr"));
    }

    [Fact]
    public void A_culture_specific_translation_wins_over_the_default()
    {
        var catalog = new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["en"] = new Dictionary<string, string> { ["title"] = "Dashboard" },
            ["tr"] = new Dictionary<string, string> { ["title"] = "Gösterge Paneli" },
        };
        var provider = Create(catalog, defaultCulture: "en");

        Assert.Equal("Gösterge Paneli", provider.Localize(new LocalizationKey("title"), "tr"));
    }
}
