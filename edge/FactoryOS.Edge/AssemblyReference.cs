namespace FactoryOS.Edge;

/// <summary>
/// Stable type reference for the <c>FactoryOS.Edge</c> assembly. Provides a single, well-known anchor
/// used for assembly scanning and architecture tests across the platform.
/// </summary>
public static class AssemblyReference
{
    /// <summary>
    /// Gets the runtime <see cref="System.Reflection.Assembly"/> for this project.
    /// </summary>
    public static System.Reflection.Assembly Assembly => typeof(AssemblyReference).Assembly;
}
