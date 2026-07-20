namespace FactoryOS.Plugins.Sample;

/// <summary>A trivial service the sample plugin contributes, proving that plugin DI wiring works.</summary>
public interface ISampleGreeter
{
    /// <summary>Produces a greeting for the given name.</summary>
    /// <param name="name">The name to greet.</param>
    /// <returns>A greeting string.</returns>
    string Greet(string name);
}
