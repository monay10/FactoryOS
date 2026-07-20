using FactoryOS.Gateway.Security;
using FactoryOS.Gateway.Ui;

namespace FactoryOS.IntegrationTests.Gateway;

public sealed class NavigationPermissionFilterTests
{
    private static NavItem Item(string module, string? permission) =>
        new(module, $"{module}.s", module, $"/{module}", $"{module}/S", null, permission, 1);

    private static readonly NavCatalog Nav = new([
        new NavSection("Experience", [Item("dashboard", null)]),
        new NavSection("Admin", [Item("store", "admin.manage")]),
    ]);

    private sealed class Perms : IPermissionContext
    {
        private readonly HashSet<string> _held;
        public Perms(bool unrestricted, params string[] held)
        {
            Unrestricted = unrestricted;
            _held = new HashSet<string>(held, StringComparer.OrdinalIgnoreCase);
        }

        public bool Unrestricted { get; }
        public IReadOnlyCollection<string> Permissions => _held;
        public bool Holds(string permission) => Unrestricted || _held.Contains(permission);
    }

    [Fact]
    public void An_unrestricted_request_sees_everything_unchanged()
    {
        var filtered = NavigationPermissionFilter.Apply(Nav, new Perms(unrestricted: true));

        Assert.Same(Nav, filtered);
    }

    [Fact]
    public void A_permissioned_screen_is_hidden_without_the_permission_and_its_empty_section_dropped()
    {
        var filtered = NavigationPermissionFilter.Apply(Nav, new Perms(unrestricted: false, "energy.view"));

        var section = Assert.Single(filtered.Sections);
        Assert.Equal("Experience", section.Section);
        Assert.Equal("dashboard", Assert.Single(section.Items).Module);
    }

    [Fact]
    public void Holding_the_permission_keeps_the_screen()
    {
        var filtered = NavigationPermissionFilter.Apply(Nav, new Perms(unrestricted: false, "admin.manage"));

        Assert.Equal(["Experience", "Admin"], filtered.Sections.Select(s => s.Section));
    }
}
