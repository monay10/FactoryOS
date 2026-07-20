using System.Runtime.CompilerServices;
using FactoryOS.Contracts.Connectors;

namespace FactoryOS.Connectors.Ldap;

/// <summary>
/// A connector for a generic LDAP directory (OpenLDAP, 389 Directory Server, …). It searches the configured
/// user and group subtrees and yields one raw <see cref="SourceRecord"/> per entry, tagged <c>USERS</c> or
/// <c>GROUPS</c>. LDAP's dialect (<c>uid</c>, <c>cn</c>, <c>mail</c>, …) is normalized to the Standard Model
/// by the mapping — the connector itself never speaks Standard Model.
/// </summary>
public sealed class LdapConnector : IConnector
{
    /// <summary>The connector key, matching <c>connector.json</c>.</summary>
    public const string ConnectorKey = "ldap";

    /// <summary>The source-entity tag for user records.</summary>
    public const string UsersEntity = "USERS";

    /// <summary>The source-entity tag for group records.</summary>
    public const string GroupsEntity = "GROUPS";

    private readonly ILdapClient _client;
    private readonly LdapConnectorOptions _options;

    /// <summary>Initializes a new instance of the <see cref="LdapConnector"/> class.</summary>
    /// <param name="client">The LDAP transport to read through.</param>
    /// <param name="options">The search bases and filters.</param>
    public LdapConnector(ILdapClient client, LdapConnectorOptions options)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.UserBaseDn);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.GroupBaseDn);
        _client = client;
        _options = options;
    }

    /// <inheritdoc />
    public string Key => ConnectorKey;

    /// <inheritdoc />
    public string SourceSystem => "LDAP";

    /// <inheritdoc />
    public async IAsyncEnumerable<SourceRecord> ReadAsync(
        ConnectorReadContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        await foreach (var entry in _client
            .SearchAsync(_options.UserBaseDn, _options.UserFilter, cancellationToken)
            .ConfigureAwait(false))
        {
            yield return ToRecord(UsersEntity, entry);
        }

        await foreach (var entry in _client
            .SearchAsync(_options.GroupBaseDn, _options.GroupFilter, cancellationToken)
            .ConfigureAwait(false))
        {
            yield return ToRecord(GroupsEntity, entry);
        }
    }

    private static SourceRecord ToRecord(string entity, LdapEntry entry)
    {
        var fields = new Dictionary<string, object?>(entry.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["dn"] = entry.Dn,
        };

        return new SourceRecord(entity, fields);
    }
}
