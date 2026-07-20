using FactoryOS.Configuration.Model;
using FactoryOS.Configuration.Reading;
using FactoryOS.Domain.Results;

namespace FactoryOS.Configuration.Providers;

/// <summary>
/// Thread-safe <see cref="ITenantConfigurationProvider"/>. It loads once eagerly so a misconfigured
/// tenant fails fast, then serves an atomically-swapped snapshot that <see cref="Reload"/> replaces
/// only when the new configuration is valid.
/// </summary>
public sealed class TenantConfigurationProvider : ITenantConfigurationProvider
{
    private readonly ITenantConfigurationSource _source;
    private readonly Lock _gate = new();
    private TenantConfiguration _current;

    /// <summary>Initializes a new instance of the <see cref="TenantConfigurationProvider"/> class.</summary>
    /// <param name="source">The source to load the configuration from.</param>
    /// <exception cref="InvalidOperationException">Thrown when the initial load fails.</exception>
    public TenantConfigurationProvider(ITenantConfigurationSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;

        var initial = source.Load();
        if (initial.IsFailure)
        {
            throw new InvalidOperationException(
                $"Failed to load the tenant configuration ({initial.Error.Code}): {initial.Error.Description}");
        }

        _current = initial.Value;
    }

    /// <inheritdoc />
    public TenantConfiguration Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler<TenantConfiguration>? Changed;

    /// <inheritdoc />
    public Result<TenantConfiguration> Reload()
    {
        var reloaded = _source.Load();
        if (reloaded.IsFailure)
        {
            return reloaded;
        }

        lock (_gate)
        {
            _current = reloaded.Value;
        }

        Changed?.Invoke(this, reloaded.Value);
        return reloaded;
    }
}
