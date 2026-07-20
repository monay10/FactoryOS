using System.Globalization;
using System.Runtime.CompilerServices;
using FactoryOS.Contracts.Connectors;

namespace FactoryOS.Connectors.ActiveDirectory;

/// <summary>
/// A connector for Microsoft Active Directory. It searches the configured user and group containers and yields
/// one raw <see cref="SourceRecord"/> per entry, tagged <c>USERS</c> or <c>GROUPS</c>. AD's dialect
/// (<c>sAMAccountName</c>, <c>userAccountControl</c>, …) is normalized to the Standard Model by the mapping.
/// The account's enabled state is derived from the <c>userAccountControl</c> <c>ACCOUNTDISABLE</c> bit and
/// surfaced as a plain <c>enabled</c> field — the mapping stays declarative.
/// </summary>
public sealed class ActiveDirectoryConnector : IConnector
{
    /// <summary>The connector key, matching <c>connector.json</c>.</summary>
    public const string ConnectorKey = "activedirectory";

    /// <summary>The source-entity tag for user records.</summary>
    public const string UsersEntity = "USERS";

    /// <summary>The source-entity tag for group records.</summary>
    public const string GroupsEntity = "GROUPS";

    // userAccountControl flag: the account is disabled when this bit is set.
    private const long AccountDisable = 0x2;

    private readonly IActiveDirectory _directory;
    private readonly ActiveDirectoryConnectorOptions _options;

    /// <summary>Initializes a new instance of the <see cref="ActiveDirectoryConnector"/> class.</summary>
    /// <param name="directory">The Active Directory transport to read through.</param>
    /// <param name="options">The search bases and filters.</param>
    public ActiveDirectoryConnector(IActiveDirectory directory, ActiveDirectoryConnectorOptions options)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.UserSearchBase);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.GroupSearchBase);
        _directory = directory;
        _options = options;
    }

    /// <inheritdoc />
    public string Key => ConnectorKey;

    /// <inheritdoc />
    public string SourceSystem => "ActiveDirectory";

    /// <inheritdoc />
    public async IAsyncEnumerable<SourceRecord> ReadAsync(
        ConnectorReadContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        await foreach (var entry in _directory
            .SearchAsync(_options.UserSearchBase, _options.UserFilter, cancellationToken)
            .ConfigureAwait(false))
        {
            var fields = new Dictionary<string, object?>(entry.Attributes, StringComparer.OrdinalIgnoreCase)
            {
                ["enabled"] = IsEnabled(entry.Attributes),
            };

            yield return new SourceRecord(UsersEntity, fields);
        }

        await foreach (var entry in _directory
            .SearchAsync(_options.GroupSearchBase, _options.GroupFilter, cancellationToken)
            .ConfigureAwait(false))
        {
            var fields = new Dictionary<string, object?>(entry.Attributes, StringComparer.OrdinalIgnoreCase);
            yield return new SourceRecord(GroupsEntity, fields);
        }
    }

    private static bool IsEnabled(IReadOnlyDictionary<string, object?> attributes)
    {
        if (!attributes.TryGetValue("userAccountControl", out var raw) || raw is null)
        {
            return true; // no flag present → treat as enabled
        }

        var text = System.Convert.ToString(raw, CultureInfo.InvariantCulture);
        if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var uac))
        {
            return true; // unparseable flag → treat as enabled rather than lock the account out
        }

        return (uac & AccountDisable) == 0;
    }
}
