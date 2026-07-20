using System.Linq.Expressions;

namespace FactoryOS.Domain.Specifications;

/// <summary>Rebinds all parameters of an expression tree to a single shared parameter.</summary>
internal sealed class ParameterReplacer : ExpressionVisitor
{
    private readonly ParameterExpression _parameter;

    public ParameterReplacer(ParameterExpression parameter)
    {
        _parameter = parameter;
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        return _parameter;
    }
}

/// <summary>Merges two boolean predicate expressions under a shared parameter.</summary>
internal static class PredicateCombiner
{
    public static Expression<Func<T, bool>> Combine<T>(
        Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right,
        Func<Expression, Expression, Expression> merge)
    {
        var parameter = Expression.Parameter(typeof(T));
        var leftBody = new ParameterReplacer(parameter).Visit(left.Body);
        var rightBody = new ParameterReplacer(parameter).Visit(right.Body);
        return Expression.Lambda<Func<T, bool>>(merge(leftBody, rightBody), parameter);
    }
}

/// <summary>The logical AND of two specifications.</summary>
/// <typeparam name="T">The type the specification applies to.</typeparam>
internal sealed class AndSpecification<T> : Specification<T>
{
    private readonly Specification<T> _left;
    private readonly Specification<T> _right;

    public AndSpecification(Specification<T> left, Specification<T> right)
    {
        _left = left;
        _right = right;
    }

    public override Expression<Func<T, bool>> ToExpression()
    {
        return PredicateCombiner.Combine(_left.ToExpression(), _right.ToExpression(), Expression.AndAlso);
    }
}

/// <summary>The logical OR of two specifications.</summary>
/// <typeparam name="T">The type the specification applies to.</typeparam>
internal sealed class OrSpecification<T> : Specification<T>
{
    private readonly Specification<T> _left;
    private readonly Specification<T> _right;

    public OrSpecification(Specification<T> left, Specification<T> right)
    {
        _left = left;
        _right = right;
    }

    public override Expression<Func<T, bool>> ToExpression()
    {
        return PredicateCombiner.Combine(_left.ToExpression(), _right.ToExpression(), Expression.OrElse);
    }
}

/// <summary>The logical negation of a specification.</summary>
/// <typeparam name="T">The type the specification applies to.</typeparam>
internal sealed class NotSpecification<T> : Specification<T>
{
    private readonly Specification<T> _inner;

    public NotSpecification(Specification<T> inner)
    {
        _inner = inner;
    }

    public override Expression<Func<T, bool>> ToExpression()
    {
        var expression = _inner.ToExpression();
        var body = Expression.Not(expression.Body);
        return Expression.Lambda<Func<T, bool>>(body, expression.Parameters[0]);
    }
}
