using FactoryOS.Connectors.Manifest;
using FactoryOS.Contracts.Connectors;

namespace FactoryOS.IntegrationTests.Connectors;

/// <summary>
/// Resolves a connector's shipped mapping manifest from its source folder. Every connector ships a
/// <c>mapping.json</c>; because all connectors are referenced into one test output directory, the files
/// are read from source rather than the (colliding) output directory.
/// </summary>
internal static class ConnectorAssets
{
    public static MappingManifest Mapping(string connectorFolder)
    {
        var path = Path.Combine(RepoRoot(), "connectors", connectorFolder, "mapping.json");
        var result = MappingManifestReader.ReadFile(path);
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Could not read mapping for '{connectorFolder}': {result.Error.Description}");
        }

        return result.Value;
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FactoryOS.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate the repository root.");
    }
}
