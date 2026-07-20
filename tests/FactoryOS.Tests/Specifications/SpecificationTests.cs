using System.Linq.Expressions;
using FactoryOS.Domain.Specifications;

namespace FactoryOS.Tests.Specifications;

public sealed class SpecificationTests
{
    private sealed record Product(string Name, decimal Price);

    private sealed class ExpensiveProduct : Specification<Product>
    {
        public override Expression<Func<Product, bool>> ToExpression()
        {
            return product => product.Price > 100m;
        }
    }

    private sealed class NamedProduct : Specification<Product>
    {
        private readonly string _name;

        public NamedProduct(string name)
        {
            _name = name;
        }

        public override Expression<Func<Product, bool>> ToExpression()
        {
            return product => product.Name == _name;
        }
    }

    [Fact]
    public void Is_satisfied_by_evaluates_the_predicate()
    {
        var spec = new ExpensiveProduct();

        Assert.True(spec.IsSatisfiedBy(new Product("Pump", 150m)));
        Assert.False(spec.IsSatisfiedBy(new Product("Bolt", 5m)));
    }

    [Fact]
    public void And_requires_both_specifications_to_hold()
    {
        var spec = new ExpensiveProduct().And(new NamedProduct("Pump"));

        Assert.True(spec.IsSatisfiedBy(new Product("Pump", 150m)));
        Assert.False(spec.IsSatisfiedBy(new Product("Pump", 5m)));
        Assert.False(spec.IsSatisfiedBy(new Product("Bolt", 150m)));
    }

    [Fact]
    public void Or_requires_at_least_one_specification_to_hold()
    {
        var spec = new ExpensiveProduct().Or(new NamedProduct("Bolt"));

        Assert.True(spec.IsSatisfiedBy(new Product("Bolt", 5m)));
        Assert.True(spec.IsSatisfiedBy(new Product("Pump", 150m)));
        Assert.False(spec.IsSatisfiedBy(new Product("Nut", 5m)));
    }

    [Fact]
    public void Not_negates_the_specification()
    {
        var spec = new ExpensiveProduct().Not();

        Assert.True(spec.IsSatisfiedBy(new Product("Bolt", 5m)));
        Assert.False(spec.IsSatisfiedBy(new Product("Pump", 150m)));
    }
}
