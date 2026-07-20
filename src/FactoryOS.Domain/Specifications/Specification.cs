using System.Linq.Expressions;

namespace FactoryOS.Domain.Specifications;

/// <summary>
/// Base class for the Specification pattern: an encapsulated, composable business rule that can be
/// evaluated in memory or translated to a query predicate.
/// </summary>
/// <typeparam name="T">The type the specification applies to.</typeparam>
public abstract class Specification<T>
{
    /// <summary>Returns the predicate expression that defines this specification.</summary>
    /// <returns>An expression usable both in memory and by query providers.</returns>
    public abstract Expression<Func<T, bool>> ToExpression();

    /// <summary>Evaluates whether the given candidate satisfies this specification.</summary>
    /// <param name="candidate">The candidate to test.</param>
    /// <returns><see langword="true"/> if the candidate satisfies the rule; otherwise <see langword="false"/>.</returns>
    public bool IsSatisfiedBy(T candidate)
    {
        return ToExpression().Compile()(candidate);
    }

    /// <summary>Combines this specification with another using a logical AND.</summary>
    /// <param name="other">The specification to combine with.</param>
    /// <returns>A composite specification.</returns>
    public Specification<T> And(Specification<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new AndSpecification<T>(this, other);
    }

    /// <summary>Combines this specification with another using a logical OR.</summary>
    /// <param name="other">The specification to combine with.</param>
    /// <returns>A composite specification.</returns>
    public Specification<T> Or(Specification<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new OrSpecification<T>(this, other);
    }

    /// <summary>Negates this specification.</summary>
    /// <returns>A specification satisfied exactly when this one is not.</returns>
    public Specification<T> Not()
    {
        return new NotSpecification<T>(this);
    }
}
