using FactoryOS.Connectors.ActiveDirectory;
using FactoryOS.Connectors.Binding;
using FactoryOS.Connectors.Deduplication;
using FactoryOS.Connectors.Normalization;
using FactoryOS.Connectors.Pipeline;
using FactoryOS.Connectors.Transforms;
using FactoryOS.Contracts.Connectors;
using FactoryOS.Contracts.StandardModel;

namespace FactoryOS.IntegrationTests.Connectors;

public sealed class ActiveDirectoryConnectorTests
{
    private sealed class FakeAd : IActiveDirectory
    {
        private readonly IReadOnlyDictionary<string, IReadOnlyList<AdEntry>> _byBase;

        public FakeAd(IReadOnlyDictionary<string, IReadOnlyList<AdEntry>> byBase) => _byBase = byBase;

        public async IAsyncEnumerable<AdEntry> SearchAsync(
            string searchBase,
            string filter,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            if (!_byBase.TryGetValue(searchBase, out var entries))
            {
                yield break;
            }

            foreach (var entry in entries)
            {
                yield return entry;
            }
        }
    }

    private static AdEntry Entry(params (string Key, object? Value)[] attributes) =>
        new(attributes.ToDictionary(a => a.Key, a => a.Value, StringComparer.OrdinalIgnoreCase));

    private static ActiveDirectoryConnector BuildConnector()
    {
        var directory = new FakeAd(new Dictionary<string, IReadOnlyList<AdEntry>>(StringComparer.Ordinal)
        {
            ["OU=Users,DC=corp,DC=example,DC=com"] =
            [
                Entry(("sAMAccountName", "EMRE"), ("displayName", "Emre Yıldız"), ("mail", "Emre@corp.example.com"), ("userAccountControl", 512)),
                Entry(("sAMAccountName", "banned"), ("displayName", "Banned User"), ("userAccountControl", 514)), // 512 | 0x2 disabled
            ],
            ["OU=Groups,DC=corp,DC=example,DC=com"] =
            [
                Entry(("sAMAccountName", "Maintenance"), ("displayName", "Maintenance Team"), ("description", "CMMS users")),
            ],
        });

        return new ActiveDirectoryConnector(directory, new ActiveDirectoryConnectorOptions
        {
            UserSearchBase = "OU=Users,DC=corp,DC=example,DC=com",
            GroupSearchBase = "OU=Groups,DC=corp,DC=example,DC=com",
        });
    }

    [Fact]
    public async Task Derives_enabled_from_user_account_control()
    {
        var records = new List<SourceRecord>();
        await foreach (var record in BuildConnector().ReadAsync(new ConnectorReadContext("acme"), CancellationToken.None))
        {
            records.Add(record);
        }

        var emre = records.Single(r => Equals(r.Fields["sAMAccountName"], "EMRE"));
        var banned = records.Single(r => Equals(r.Fields["sAMAccountName"], "banned"));
        Assert.Equal(true, emre.Fields["enabled"]);
        Assert.Equal(false, banned.Fields["enabled"]); // ACCOUNTDISABLE bit set
    }

    [Fact]
    public async Task Feeds_the_ingestion_pipeline_into_standard_model_identity_entities()
    {
        var mapping = ConnectorAssets.Mapping("activedirectory");
        var pipeline = new IngestionPipeline(new RecordNormalizer(new ValueTransformer()), new RecordDeduplicator());

        var result = await pipeline.RunAsync(BuildConnector(), mapping, new ConnectorReadContext("acme"), CancellationToken.None);

        Assert.Empty(result.Errors);

        var binder = new StandardEntityBinder();
        var emre = Assert.IsType<DirectoryUser>(binder.Bind(result.Records.Single(r => r.NaturalKey == "emre")).Value);
        Assert.Equal("emre@corp.example.com", emre.Email);
        Assert.True(emre.Enabled);

        var banned = Assert.IsType<DirectoryUser>(binder.Bind(result.Records.Single(r => r.NaturalKey == "banned")).Value);
        Assert.False(banned.Enabled);

        var group = Assert.IsType<DirectoryGroup>(binder.Bind(result.Records.Single(r => r.NaturalKey == "Maintenance")).Value);
        Assert.Equal("Maintenance Team", group.DisplayName);
    }
}
