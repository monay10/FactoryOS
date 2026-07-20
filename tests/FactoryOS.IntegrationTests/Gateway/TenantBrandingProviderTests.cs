using FactoryOS.Gateway.Branding;

namespace FactoryOS.IntegrationTests.Gateway;

public sealed class TenantBrandingProviderTests
{
    [Fact]
    public void An_unknown_tenant_falls_back_to_neutral_branding()
    {
        var provider = new TenantBrandingProvider();

        var branding = provider.ForTenant("nobody");

        Assert.Equal("nobody", branding.Tenant);
        Assert.Equal("nobody", branding.DisplayName);
        Assert.Null(branding.PrimaryColor);
    }

    [Fact]
    public void A_seeded_tenant_resolves_its_branding_case_insensitively()
    {
        var provider = new TenantBrandingProvider([new TenantBranding("acme", "Acme Foods", "#e11d48")]);

        var branding = provider.ForTenant("ACME");

        Assert.Equal("Acme Foods", branding.DisplayName);
        Assert.Equal("#e11d48", branding.PrimaryColor);
    }
}
