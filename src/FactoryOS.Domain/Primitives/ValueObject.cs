namespace FactoryOS.Domain.Primitives;

/// <summary>
/// Base class for value objects — immutable types without identity whose equality is defined by the
/// equality of their components.
/// </summary>
public abstract class ValueObject : IEquatable<ValueObject>
{
    /// <summary>Determines whether two value objects are equal.</summary>
    /// <param name="left">The first value object.</param>
    /// <param name="right">The second value object.</param>
    /// <returns><see langword="true"/> if the value objects are equal; otherwise <see langword="false"/>.</returns>
    public static bool operator ==(ValueObject? left, ValueObject? right)
    {
        return Equals(left, right);
    }

    /// <summary>Determines whether two value objects are not equal.</summary>
    /// <param name="left">The first value object.</param>
    /// <param name="right">The second value object.</param>
    /// <returns><see langword="true"/> if the value objects differ; otherwise <see langword="false"/>.</returns>
    public static bool operator !=(ValueObject? left, ValueObject? right)
    {
        return !Equals(left, right);
    }

    /// <inheritdoc />
    public bool Equals(ValueObject? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return GetType() == other.GetType()
            && GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is ValueObject valueObject && Equals(valueObject);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hashCode = default(HashCode);

        foreach (var component in GetEqualityComponents())
        {
            hashCode.Add(component);
        }

        return hashCode.ToHashCode();
    }

    /// <summary>Returns the ordered components that participate in equality comparisons.</summary>
    /// <returns>The sequence of equality components.</returns>
    protected abstract IEnumerable<object?> GetEqualityComponents();
}
