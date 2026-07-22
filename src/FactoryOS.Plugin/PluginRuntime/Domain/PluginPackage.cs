using System.Globalization;
using FactoryOS.Contracts.Plugins;

namespace FactoryOS.Plugins.Runtime.Domain;

/// <summary>
/// A package's detached signature: which key produced it, with which algorithm, and the value itself.
/// <para>
/// The signature is <b>detached</b> — it sits beside the package rather than inside it — so verifying it
/// never requires opening, extracting or loading anything the signature was supposed to vouch for.
/// </para>
/// </summary>
/// <param name="Algorithm">The algorithm the signature was produced with.</param>
/// <param name="Value">The signature value, Base64-encoded.</param>
public sealed record PluginSignature(PluginSignatureAlgorithm Algorithm, string Value)
{
    /// <summary>Gets the absent signature.</summary>
    public static PluginSignature None { get; } = new(PluginSignatureAlgorithm.None, string.Empty);

    /// <summary>Gets the identifier of the key that produced the signature.</summary>
    public string? KeyId { get; init; }

    /// <summary>Gets a value indicating whether a signature is actually present.</summary>
    public bool IsPresent =>
        Algorithm != PluginSignatureAlgorithm.None && !string.IsNullOrWhiteSpace(Value);

    /// <summary>Builds an HMAC-SHA256 signature.</summary>
    /// <param name="value">The Base64 signature value.</param>
    /// <param name="keyId">The identifier of the key that produced it.</param>
    /// <returns>The signature.</returns>
    public static PluginSignature Hmac(string value, string keyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);
        return new PluginSignature(PluginSignatureAlgorithm.HmacSha256, value) { KeyId = keyId };
    }

    /// <inheritdoc />
    public override string ToString() => IsPresent
        ? string.Create(CultureInfo.InvariantCulture, $"{Algorithm}:{KeyId}")
        : "unsigned";
}

/// <summary>
/// One installable unit: a definition at a version, the signature vouching for it, and where its content
/// lives. A package is immutable — updating a plugin produces a <i>new</i> package rather than mutating one,
/// which is precisely what makes a rollback possible.
/// </summary>
/// <param name="Manifest">The manifest exactly as it was written.</param>
/// <param name="Definition">The runtime's projection of it.</param>
/// <param name="Signature">The signature vouching for it, or <see cref="PluginSignature.None"/>.</param>
public sealed record PluginPackage(
    PluginManifest Manifest, PluginDefinition Definition, PluginSignature Signature)
{
    /// <summary>Gets the plugin key.</summary>
    public string Key => Definition.Key;

    /// <summary>Gets the packaged version.</summary>
    public PluginVersion Version => Definition.Version;

    /// <summary>Gets the folder the package content lives in, if any.</summary>
    public string? Location => Definition.Location;

    /// <summary>Gets the identity the package store files it under.</summary>
    public string Identity => Definition.Identity;

    /// <summary>Gets a value indicating whether the package carries a signature at all.</summary>
    public bool IsSigned => Signature.IsPresent;

    /// <summary>Builds a package that carries no signature.</summary>
    /// <param name="manifest">The manifest.</param>
    /// <param name="definition">The definition projected from it.</param>
    /// <returns>The package.</returns>
    public static PluginPackage WithoutSignature(PluginManifest manifest, PluginDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(definition);
        return new PluginPackage(manifest, definition, PluginSignature.None);
    }

    /// <summary>
    /// Gets the canonical content a signature is computed over: the identity-bearing fields of the package.
    /// <para>
    /// It is deliberately the <i>manifest's</i> claims rather than the assembly bytes, because those claims —
    /// key, version, entry type, requested permissions, contributions — are what the runtime acts on. A
    /// signature over the bytes alone would still let someone re-point a verified assembly at a different
    /// entry type or widen the permissions it asks for.
    /// </para>
    /// </summary>
    /// <returns>The canonical content.</returns>
    public string CanonicalContent()
    {
        var contributions = string.Join(
            ',', Definition.Contributions.Select(contribution => contribution.ToString()).Order(StringComparer.Ordinal));
        var permissions = string.Join(
            ',', Definition.EffectiveRequests().Select(permission => permission.ToString()).Order(StringComparer.Ordinal));

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Definition.Key}\n{Definition.Version}\n{Definition.Assembly}\n{Definition.EntryType}\n"
            + $"{Definition.Isolation}\n{contributions}\n{permissions}");
    }

    /// <inheritdoc />
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Identity} ({Signature})");
}
