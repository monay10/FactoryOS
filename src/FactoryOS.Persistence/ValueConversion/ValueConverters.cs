using System.Globalization;
using FactoryOS.Shared.Identifiers;
using FactoryOS.Shared.Primitives;
using FactoryOS.Shared.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FactoryOS.Persistence.ValueConversion;

/// <summary>Coerces every stored <see cref="DateTime"/> to UTC, so a value read back always has UTC kind.</summary>
public sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    /// <summary>Initializes a new instance of the <see cref="UtcDateTimeConverter"/> class.</summary>
    public UtcDateTimeConverter()
        : base(
            value => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime(),
            value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
    {
    }
}

/// <summary>Persists <see cref="Money"/> as a single canonical <c>amount|currency</c> string.</summary>
public sealed class MoneyConverter : ValueConverter<Money, string>
{
    /// <summary>Initializes a new instance of the <see cref="MoneyConverter"/> class.</summary>
    public MoneyConverter()
        : base(
            money => money.Amount.ToString(CultureInfo.InvariantCulture) + "|" + money.Currency,
            value => FromStore(value))
    {
    }

    private static Money FromStore(string value)
    {
        var separator = value.IndexOf('|', StringComparison.Ordinal);
        var amount = decimal.Parse(value.AsSpan(0, separator), CultureInfo.InvariantCulture);
        var currency = value[(separator + 1)..];
        return Money.Of(amount, currency);
    }
}

/// <summary>Persists <see cref="Percentage"/> as its underlying ratio (<c>1.0</c> = 100%).</summary>
public sealed class PercentageConverter : ValueConverter<Percentage, decimal>
{
    /// <summary>Initializes a new instance of the <see cref="PercentageConverter"/> class.</summary>
    public PercentageConverter()
        : base(percentage => percentage.Ratio, ratio => Percentage.FromRatio(ratio))
    {
    }
}

/// <summary>Persists a <see cref="DateRange"/> as a single round-trip <c>start|end</c> string.</summary>
public sealed class DateRangeConverter : ValueConverter<DateRange, string>
{
    /// <summary>Initializes a new instance of the <see cref="DateRangeConverter"/> class.</summary>
    public DateRangeConverter()
        : base(
            range => range.Start.ToString("O", CultureInfo.InvariantCulture)
                + "|" + range.End.ToString("O", CultureInfo.InvariantCulture),
            value => FromStore(value))
    {
    }

    private static DateRange FromStore(string value)
    {
        var separator = value.IndexOf('|', StringComparison.Ordinal);
        var start = DateTimeOffset.Parse(value.AsSpan(0, separator), CultureInfo.InvariantCulture);
        var end = DateTimeOffset.Parse(value.AsSpan(separator + 1), CultureInfo.InvariantCulture);
        return DateRange.Between(start, end);
    }
}

/// <summary>Persists a <see cref="Period"/> as a single <c>start|end</c> ISO-date string.</summary>
public sealed class PeriodConverter : ValueConverter<Period, string>
{
    /// <summary>Initializes a new instance of the <see cref="PeriodConverter"/> class.</summary>
    public PeriodConverter()
        : base(
            period => period.Start.ToString("O", CultureInfo.InvariantCulture)
                + "|" + period.End.ToString("O", CultureInfo.InvariantCulture),
            value => FromStore(value))
    {
    }

    private static Period FromStore(string value)
    {
        var separator = value.IndexOf('|', StringComparison.Ordinal);
        var start = DateOnly.Parse(value.AsSpan(0, separator), CultureInfo.InvariantCulture);
        var end = DateOnly.Parse(value.AsSpan(separator + 1), CultureInfo.InvariantCulture);
        return Period.Between(start, end);
    }
}

/// <summary>Persists an <see cref="Enumeration"/> member as its stable integer <see cref="Enumeration.Id"/>.</summary>
/// <typeparam name="TEnumeration">The enumeration type.</typeparam>
public sealed class EnumerationConverter<TEnumeration> : ValueConverter<TEnumeration, int>
    where TEnumeration : Enumeration
{
    /// <summary>Initializes a new instance of the <see cref="EnumerationConverter{TEnumeration}"/> class.</summary>
    public EnumerationConverter()
        : base(member => member.Id, id => Enumeration.FromId<TEnumeration>(id))
    {
    }
}
