using FactoryOS.Domain.Results;
using FactoryOS.Plugins.Runtime.Configuration;
using FactoryOS.Plugins.Runtime.Discovery;
using FactoryOS.Plugins.Runtime.Domain;
using Microsoft.Extensions.Options;

namespace FactoryOS.Plugins.Runtime.Security;

/// <summary>
/// Everything a package must pass before a tenant installs it, applied in one fixed order.
/// <para>
/// The order is the point, and two of the steps are placed deliberately.
/// </para>
/// <para>
/// <b>The signature is verified before anything acts on the package's claims</b>, and — because installation
/// always precedes loading — before a single byte of the plugin's code is loaded into the process. You cannot
/// un-run code: a check performed after loading has already lost, whatever it decides.
/// </para>
/// <para>
/// <b>The permission grant is checked last</b>, because it is the only step whose answer is a property of the
/// tenant rather than the package. Refusing an incompatible or unsigned package with "you have not granted
/// it enough" would send an operator to the wrong screen entirely.
/// </para>
/// </summary>
public sealed class PluginValidationSuite
{
    private readonly PluginManifestValidator _manifest;
    private readonly PluginSignatureValidator _signature;
    private readonly PluginCompatibilityValidator _compatibility;
    private readonly PluginPermissionValidator _permissions;
    private readonly PluginCapabilityRequirementValidator _capabilities;
    private readonly PluginRuntimeOptions _options;

    /// <summary>Initializes a new instance of the <see cref="PluginValidationSuite"/> class.</summary>
    /// <param name="manifest">The manifest validator.</param>
    /// <param name="signature">The signature validator.</param>
    /// <param name="compatibility">The compatibility validator.</param>
    /// <param name="permissions">The permission validator.</param>
    /// <param name="capabilities">The capability validator.</param>
    /// <param name="options">The runtime options.</param>
    public PluginValidationSuite(
        PluginManifestValidator manifest,
        PluginSignatureValidator signature,
        PluginCompatibilityValidator compatibility,
        PluginPermissionValidator permissions,
        PluginCapabilityRequirementValidator capabilities,
        IOptions<PluginRuntimeOptions> options)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(compatibility);
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(options);

        _manifest = manifest;
        _signature = signature;
        _compatibility = compatibility;
        _permissions = permissions;
        _capabilities = capabilities;
        _options = options.Value;
    }

    /// <summary>Gets the platform version packages are validated against.</summary>
    public Contracts.Plugins.PluginVersion Platform => _options.Platform();

    /// <summary>Validates a package a tenant is about to install.</summary>
    /// <param name="package">The package.</param>
    /// <param name="instance">The installation the tenant would end up with.</param>
    /// <param name="providers">The definitions already installed for the tenant, which may satisfy requirements.</param>
    /// <returns>A successful result, or the first failure found.</returns>
    public Result ValidateForInstall(
        PluginPackage package, PluginInstance instance, IReadOnlyList<PluginDefinition> providers)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(providers);

        var manifest = _manifest.Validate(package.Definition);
        if (manifest.IsFailure)
        {
            return manifest;
        }

        var signature = _signature.Validate(package);
        if (signature.IsFailure)
        {
            return signature;
        }

        var compatibility = _compatibility.Validate(package.Definition, Platform);
        if (compatibility.IsFailure)
        {
            return compatibility;
        }

        var capabilities = _capabilities.Validate(package.Definition, providers);
        return capabilities.IsFailure ? capabilities : ValidatePermissions(instance, package.Definition);
    }

    /// <summary>Validates that a tenant has granted everything a definition asks for.</summary>
    /// <param name="instance">The tenant's installation.</param>
    /// <param name="definition">The definition it installs.</param>
    /// <returns>A successful result, or a failure naming what is ungranted.</returns>
    public Result ValidatePermissions(PluginInstance instance, PluginDefinition definition) =>
        _permissions.Validate(instance, definition);
}
