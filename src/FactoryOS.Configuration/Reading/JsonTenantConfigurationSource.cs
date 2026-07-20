using FactoryOS.Configuration.Model;
using FactoryOS.Domain.Results;

namespace FactoryOS.Configuration.Reading;

/// <summary>An <see cref="ITenantConfigurationSource"/> backed by a <c>tenant.json</c> file on disk.</summary>
public sealed class JsonTenantConfigurationSource : ITenantConfigurationSource
{
    private readonly string _path;
    private readonly TenantConfigurationReader _reader;

    /// <summary>Initializes a new instance of the <see cref="JsonTenantConfigurationSource"/> class.</summary>
    /// <param name="path">The path to the tenant configuration file.</param>
    /// <param name="reader">The reader used to parse and validate the file.</param>
    public JsonTenantConfigurationSource(string path, TenantConfigurationReader reader)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(reader);

        _path = path;
        _reader = reader;
    }

    /// <inheritdoc />
    public Result<TenantConfiguration> Load()
    {
        if (!File.Exists(_path))
        {
            return Result.Failure<TenantConfiguration>(
                Error.NotFound("Configuration.Tenant.NotFound", $"No tenant configuration was found at '{_path}'."));
        }

        return _reader.Read(File.ReadAllText(_path));
    }
}
