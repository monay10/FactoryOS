using System.Globalization;
using FactoryOS.Shared.Guards;

namespace FactoryOS.Shared.ValueObjects;

/// <summary>
/// A monetary amount in a specific ISO 4217 currency. Immutable with value equality. Arithmetic is only defined
/// between amounts of the same currency; mixing currencies throws rather than silently converting.
/// </summary>
public sealed record Money
{
    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>Gets the amount.</summary>
    public decimal Amount { get; }

    /// <summary>Gets the ISO 4217 currency code (three uppercase letters).</summary>
    public string Currency { get; }

    /// <summary>Creates a monetary amount.</summary>
    /// <param name="amount">The amount.</param>
    /// <param name="currency">The ISO 4217 currency code (three letters).</param>
    /// <returns>A new <see cref="Money"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the currency code is not three letters.</exception>
    public static Money Of(decimal amount, string currency)
    {
        Guard.AgainstNullOrWhiteSpace(currency);
        if (currency.Length != 3)
        {
            throw new ArgumentException("An ISO 4217 currency code must be exactly three letters.", nameof(currency));
        }

        return new Money(amount, currency.ToUpperInvariant());
    }

    /// <summary>Creates a zero amount in a currency.</summary>
    /// <param name="currency">The ISO 4217 currency code.</param>
    /// <returns>A zero-valued <see cref="Money"/>.</returns>
    public static Money Zero(string currency) => Of(0m, currency);

    /// <summary>Adds two amounts of the same currency.</summary>
    /// <param name="other">The amount to add.</param>
    /// <returns>The sum.</returns>
    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    /// <summary>Subtracts an amount of the same currency.</summary>
    /// <param name="other">The amount to subtract.</param>
    /// <returns>The difference.</returns>
    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    /// <summary>Multiplies the amount by a scalar factor.</summary>
    /// <param name="factor">The factor.</param>
    /// <returns>The scaled amount.</returns>
    public Money Multiply(decimal factor) => new(Amount * factor, Currency);

    /// <summary>Adds two amounts of the same currency.</summary>
    /// <param name="left">The first amount.</param>
    /// <param name="right">The second amount.</param>
    /// <returns>The sum.</returns>
    public static Money operator +(Money left, Money right)
    {
        Guard.AgainstNull(left);
        return left.Add(right);
    }

    /// <summary>Subtracts one amount from another of the same currency.</summary>
    /// <param name="left">The minuend.</param>
    /// <param name="right">The subtrahend.</param>
    /// <returns>The difference.</returns>
    public static Money operator -(Money left, Money right)
    {
        Guard.AgainstNull(left);
        return left.Subtract(right);
    }

    /// <summary>Multiplies an amount by a scalar factor.</summary>
    /// <param name="left">The amount.</param>
    /// <param name="factor">The factor.</param>
    /// <returns>The scaled amount.</returns>
    public static Money operator *(Money left, decimal factor)
    {
        Guard.AgainstNull(left);
        return left.Multiply(factor);
    }

    /// <inheritdoc />
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Amount:0.00} {Currency}");

    private void EnsureSameCurrency(Money other)
    {
        Guard.AgainstNull(other);
        if (!string.Equals(Currency, other.Currency, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Cannot operate on amounts in different currencies ('{Currency}' and '{other.Currency}').");
        }
    }
}
