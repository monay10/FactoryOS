using System.Reflection;

namespace FactoryOS.Shared.Primitives;

/// <summary>
/// Base class for strongly-typed enumerations ("smart enums") — richer than a CLR <see langword="enum"/> because each
/// member is a full object that can carry behavior and extra data while keeping a stable integer <see cref="Id"/> and
/// a display <see cref="Name"/>. Members are declared as <c>public static readonly</c> fields on the derived type.
/// </summary>
public abstract class Enumeration : IEquatable<Enumeration>
{
    /// <summary>Initializes a new instance of the <see cref="Enumeration"/> class.</summary>
    /// <param name="id">The stable integer identifier.</param>
    /// <param name="name">The display name.</param>
    protected Enumeration(int id, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Id = id;
        Name = name;
    }

    /// <summary>Gets the stable integer identifier.</summary>
    public int Id { get; }

    /// <summary>Gets the display name.</summary>
    public string Name { get; }

    /// <summary>Returns every declared member of an enumeration type.</summary>
    /// <typeparam name="T">The enumeration type.</typeparam>
    /// <returns>All members declared as public static readonly fields.</returns>
    public static IReadOnlyList<T> GetAll<T>()
        where T : Enumeration
    {
        return typeof(T)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(field => field.GetValue(null))
            .OfType<T>()
            .ToArray();
    }

    /// <summary>Finds the member with a given identifier.</summary>
    /// <typeparam name="T">The enumeration type.</typeparam>
    /// <param name="id">The identifier to find.</param>
    /// <returns>The matching member.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no member has the identifier.</exception>
    public static T FromId<T>(int id)
        where T : Enumeration
    {
        return GetAll<T>().FirstOrDefault(member => member.Id == id)
            ?? throw new InvalidOperationException($"No {typeof(T).Name} has id {id}.");
    }

    /// <inheritdoc />
    public bool Equals(Enumeration? other) =>
        other is not null && GetType() == other.GetType() && Id == other.Id;

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as Enumeration);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    /// <inheritdoc />
    public override string ToString() => Name;
}
