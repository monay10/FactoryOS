using System.Globalization;

namespace FactoryOS.Connectors.Framework.Runtime;

/// <summary>
/// The kinds of interaction a connector supports, combinable as flags. These describe the connector's
/// capability surface — reading data in, writing data out, delivering or consuming events, executing
/// commands, transferring files and streaming — independent of the specific source system.
/// </summary>
[Flags]
public enum ConnectorCapability
{
    /// <summary>No capabilities.</summary>
    None = 0,

    /// <summary>Reads data from the source system.</summary>
    Read = 1,

    /// <summary>Writes data to the target system.</summary>
    Write = 1 << 1,

    /// <summary>Produces or consumes events.</summary>
    Events = 1 << 2,

    /// <summary>Executes commands against the source system.</summary>
    Commands = 1 << 3,

    /// <summary>Transfers files.</summary>
    Files = 1 << 4,

    /// <summary>Streams data continuously.</summary>
    Streaming = 1 << 5,
}

/// <summary>Helpers for parsing and testing <see cref="ConnectorCapability"/> flag sets.</summary>
public static class ConnectorCapabilities
{
    /// <summary>Determines whether a capability set includes a requested capability.</summary>
    /// <param name="declared">The declared capability set.</param>
    /// <param name="required">The capability to test for.</param>
    /// <returns><see langword="true"/> when every bit of <paramref name="required"/> is present.</returns>
    public static bool Supports(this ConnectorCapability declared, ConnectorCapability required) =>
        (declared & required) == required && required != ConnectorCapability.None;

    /// <summary>Parses a comma- or pipe-separated capability list (e.g. <c>Read,Events</c>).</summary>
    /// <param name="value">The capability list.</param>
    /// <returns>The combined capability flags (<see cref="ConnectorCapability.None"/> when empty).</returns>
    public static ConnectorCapability Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ConnectorCapability.None;
        }

        var result = ConnectorCapability.None;
        foreach (var token in value.Split([',', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Enum.TryParse<ConnectorCapability>(token, ignoreCase: true, out var capability))
            {
                result |= capability;
            }
            else
            {
                throw new FormatException(
                    string.Create(CultureInfo.InvariantCulture, $"'{token}' is not a valid connector capability."));
            }
        }

        return result;
    }
}
