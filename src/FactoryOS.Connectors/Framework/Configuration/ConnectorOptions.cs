namespace FactoryOS.Connectors.Framework.Configuration;

/// <summary>
/// Stable constants for the FactoryOS connector platform: configuration section names, the secret prefix
/// and the default health/heartbeat values shared by the options types, services and sample configuration.
/// </summary>
public static class ConnectorConstants
{
    /// <summary>The root configuration section the <see cref="ConnectorOptions"/> bind from.</summary>
    public const string ConfigurationSection = "Connectors";

    /// <summary>The configuration section the discovery options bind from.</summary>
    public const string DiscoverySection = "Connectors:Discovery";

    /// <summary>The configuration section the health options bind from.</summary>
    public const string HealthSection = "Connectors:Health";

    /// <summary>The configuration section the security options bind from.</summary>
    public const string SecuritySection = "Connectors:Security";

    /// <summary>The configuration section per-connector configuration is read from (child sections keyed by connector key).</summary>
    public const string ConnectorConfigurationSection = "Connectors:Configuration";

    /// <summary>The special per-connector configuration key that toggles a connector on or off.</summary>
    public const string EnabledKey = "Enabled";

    /// <summary>The prefix marking a configuration value as an encrypted secret.</summary>
    public const string SecretPrefix = "enc:";

    /// <summary>The default heartbeat interval, in seconds, a healthy connector is expected to beat within.</summary>
    public const int DefaultHeartbeatIntervalSeconds = 30;

    /// <summary>The default number of missed heartbeats after which a connector is considered unhealthy.</summary>
    public const int DefaultUnhealthyAfterMissedHeartbeats = 3;

    /// <summary>The default number of consecutive failures that latches a connector into the faulted state.</summary>
    public const int DefaultFailureThreshold = 3;
}

/// <summary>Connector discovery options, bound from <see cref="ConnectorConstants.DiscoverySection"/>.</summary>
public sealed class ConnectorDiscoveryOptions
{
    /// <summary>Gets or sets a value indicating whether directory discovery runs.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the root directory whose immediate subfolders each hold a connector manifest.</summary>
    public string RootPath { get; set; } = "connectors";
}

/// <summary>Connector health/heartbeat options, bound from <see cref="ConnectorConstants.HealthSection"/>.</summary>
public sealed class ConnectorHealthOptions
{
    /// <summary>Gets or sets the heartbeat interval, in seconds.</summary>
    public int HeartbeatIntervalSeconds { get; set; } = ConnectorConstants.DefaultHeartbeatIntervalSeconds;

    /// <summary>Gets or sets the number of missed heartbeats after which a connector reads as unhealthy.</summary>
    public int UnhealthyAfterMissedHeartbeats { get; set; } = ConnectorConstants.DefaultUnhealthyAfterMissedHeartbeats;

    /// <summary>Gets or sets the number of consecutive failures that latches a connector as faulted.</summary>
    public int FailureThreshold { get; set; } = ConnectorConstants.DefaultFailureThreshold;
}

/// <summary>
/// Connector security options, bound from <see cref="ConnectorConstants.SecuritySection"/>. The
/// <see cref="EncryptionKey"/> is a secret supplied out-of-band (never committed); when present, encrypted
/// connector settings are decrypted with it, otherwise settings are treated as plaintext.
/// </summary>
public sealed class ConnectorSecurityOptions
{
    /// <summary>Gets or sets the base64-encoded AES key used to decrypt encrypted connector settings.</summary>
    public string? EncryptionKey { get; set; }
}

/// <summary>
/// The connector platform options, bound from <see cref="ConnectorConstants.ConfigurationSection"/>. The
/// <see cref="Discovery"/>, <see cref="Health"/> and <see cref="Security"/> children map to their nested sections.
/// </summary>
public sealed class ConnectorOptions
{
    /// <summary>Gets or sets a value indicating whether connectors auto-connect after they are initialized.</summary>
    public bool AutoConnect { get; set; } = true;

    /// <summary>Gets or sets the discovery options.</summary>
    public ConnectorDiscoveryOptions Discovery { get; set; } = new();

    /// <summary>Gets or sets the health options.</summary>
    public ConnectorHealthOptions Health { get; set; } = new();

    /// <summary>Gets or sets the security options.</summary>
    public ConnectorSecurityOptions Security { get; set; } = new();
}
