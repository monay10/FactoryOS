using FactoryOS.Connectors.Binding;
using FactoryOS.Connectors.Deduplication;
using FactoryOS.Connectors.Ldap;
using FactoryOS.Connectors.Normalization;
using FactoryOS.Connectors.Pipeline;
using FactoryOS.Connectors.Transforms;
using FactoryOS.Contracts.Connectors;
using FactoryOS.Contracts.StandardModel;

namespace FactoryOS.IntegrationTests.Connectors;

public sealed class LdapConnectorTests
{
    private sealed class FakeLdap : ILdapClient
    {
        private readonly IReadOnlyDictionary<string, IReadOnlyList<LdapEntry>> _byBase;

        public FakeLdap(IReadOnlyDictionary<string, IReadOnlyList<LdapEntry>> byBase) => _byBase = byBase;

        public async IAsyncEnumerable<LdapEntry> SearchAsync(
            string baseDn,
            string filter,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            if (!_byBase.TryGetValue(baseDn, out var entries))
            {
                yield break;
            }

            foreach (var entry in entries)
            {
                yield return entry;
            }
        }
    }

    private static LdapEntry Entry(string dn, params (string Key, object? Value)[] attributes) =>
        new(dn, attributes.ToDictionary(a => a.Key, a => a.Value, StringComparer.OrdinalIgnoreCase));

    private static LdapConnector BuildConnector()
    {
        var client = new FakeLdap(new Dictionary<string, IReadOnlyList<LdapEntry>>(StringComparer.Ordinal)
        {
            ["ou=people,dc=example,dc=com"] =
            [
                Entry("uid=asli,ou=people,dc=example,dc=com", ("uid", "ASLI"), ("cn", " Aslı Kaya "), ("mail", "Asli.Kaya@Example.com")),
                Entry("uid=deniz,ou=people,dc=example,dc=com", ("uid", "deniz"), ("cn", "Deniz Ada")),
            ],
            ["ou=groups,dc=example,dc=com"] =
            [
                Entry("cn=operators,ou=groups,dc=example,dc=com", ("cn", "operators"), ("description", "Shift operators")),
            ],
        });

        return new LdapConnector(client, new LdapConnectorOptions
        {
            UserBaseDn = "ou=people,dc=example,dc=com",
            GroupBaseDn = "ou=groups,dc=example,dc=com",
        });
    }

    [Fact]
    public async Task Reads_users_then_groups_tagged_by_entity()
    {
        var records = new List<SourceRecord>();
        await foreach (var record in BuildConnector().ReadAsync(new ConnectorReadContext("acme"), CancellationToken.None))
        {
            records.Add(record);
        }

        Assert.Equal(3, records.Count);
        Assert.Equal(LdapConnector.UsersEntity, records[0].SourceEntity);
        Assert.Equal(LdapConnector.GroupsEntity, records[2].SourceEntity);
        Assert.Equal("ASLI", records[0].Fields["uid"]);
    }

    [Fact]
    public async Task Feeds_the_ingestion_pipeline_into_standard_model_identity_entities()
    {
        var mapping = ConnectorAssets.Mapping("ldap");
        var pipeline = new IngestionPipeline(new RecordNormalizer(new ValueTransformer()), new RecordDeduplicator());

        var result = await pipeline.RunAsync(BuildConnector(), mapping, new ConnectorReadContext("acme"), CancellationToken.None);

        Assert.Empty(result.Errors);
        Assert.Equal(3, result.Deduplicated);

        var binder = new StandardEntityBinder();
        var user = Assert.IsType<DirectoryUser>(binder.Bind(result.Records.Single(r => r.NaturalKey == "asli")).Value);
        Assert.Equal("Aslı Kaya", user.DisplayName);       // trimmed
        Assert.Equal("asli.kaya@example.com", user.Email);  // lower-cased
        Assert.True(user.Enabled);                          // constant true

        var group = Assert.IsType<DirectoryGroup>(binder.Bind(result.Records.Single(r => r.NaturalKey == "operators")).Value);
        Assert.Equal("Shift operators", group.Description);
    }
}
