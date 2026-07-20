namespace FactoryOS.Plugin;

/// <summary>
/// Stable type reference for the <c>FactoryOS.Plugin</c> assembly. Provides a single, well-known
/// anchor used for assembly scanning and dependency-injection registration across the platform.
/// </summary>
public static class AssemblyReference
{
    /// <summary>
    /// Gets the runtime <see cref="System.Reflection.Assembly"/> for this project.
    /// </summary>
    public static System.Reflection.Assembly Assembly => typeof(AssemblyReference).Assembly;
}
