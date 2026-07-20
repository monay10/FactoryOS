using System.Globalization;

namespace FactoryOS.Plugins.Sample;

/// <summary>Default <see cref="ISampleGreeter"/> implementation.</summary>
public sealed class SampleGreeter : ISampleGreeter
{
    /// <inheritdoc />
    public string Greet(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return string.Create(CultureInfo.InvariantCulture, $"Hello, {name}, from the FactoryOS sample plugin.");
    }
}
