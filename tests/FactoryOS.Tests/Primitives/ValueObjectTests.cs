using FactoryOS.Domain.Primitives;

namespace FactoryOS.Tests.Primitives;

public sealed class ValueObjectTests
{
    private sealed class Money : ValueObject
    {
        public Money(decimal amount, string currency)
        {
            Amount = amount;
            Currency = currency;
        }

        public decimal Amount { get; }

        public string Currency { get; }

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Amount;
            yield return Currency;
        }
    }

    [Fact]
    public void Value_objects_with_equal_components_are_equal()
    {
        var first = new Money(10.5m, "EUR");
        var second = new Money(10.5m, "EUR");

        Assert.Equal(first, second);
        Assert.True(first == second);
    }

    [Fact]
    public void Value_objects_with_different_components_are_not_equal()
    {
        Assert.NotEqual(new Money(10.5m, "EUR"), new Money(10.5m, "USD"));
        Assert.NotEqual(new Money(10.5m, "EUR"), new Money(11.0m, "EUR"));
    }

    [Fact]
    public void Equal_value_objects_share_a_hash_code()
    {
        Assert.Equal(new Money(10.5m, "EUR").GetHashCode(), new Money(10.5m, "EUR").GetHashCode());
    }

    [Fact]
    public void Value_object_is_not_equal_to_null()
    {
        var money = new Money(1m, "EUR");

        Assert.False(money.Equals(null));
        Assert.True(money != null);
    }
}
