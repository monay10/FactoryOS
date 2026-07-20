using FactoryOS.Shared.ValueObjects;

namespace FactoryOS.Tests.Shared;

public sealed class MoneyTests
{
    [Fact]
    public void Amounts_with_the_same_value_and_currency_are_equal()
    {
        Assert.Equal(Money.Of(10m, "usd"), Money.Of(10m, "USD"));
    }

    [Fact]
    public void Currency_is_normalized_to_uppercase()
    {
        Assert.Equal("EUR", Money.Of(1m, "eur").Currency);
    }

    [Fact]
    public void A_currency_code_must_be_three_letters()
    {
        Assert.Throws<ArgumentException>(() => Money.Of(1m, "US"));
    }

    [Fact]
    public void Addition_sums_amounts_of_the_same_currency()
    {
        var sum = Money.Of(10m, "USD") + Money.Of(5m, "USD");

        Assert.Equal(Money.Of(15m, "USD"), sum);
    }

    [Fact]
    public void Mixing_currencies_is_rejected()
    {
        Assert.Throws<InvalidOperationException>(() => Money.Of(10m, "USD").Add(Money.Of(5m, "EUR")));
    }
}

public sealed class PercentageTests
{
    [Fact]
    public void A_ratio_and_the_equivalent_percent_are_equal()
    {
        Assert.Equal(Percentage.FromRatio(0.15m), Percentage.FromPercent(15m));
    }

    [Fact]
    public void Of_applies_the_percentage_to_a_value()
    {
        Assert.Equal(30m, Percentage.FromPercent(15m).Of(200m));
    }

    [Fact]
    public void A_negative_percentage_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Percentage.FromPercent(-1m));
    }
}

public sealed class DateRangeTests
{
    private static readonly DateTimeOffset Start = new(2026, 7, 20, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = new(2026, 7, 20, 17, 0, 0, TimeSpan.Zero);

    [Fact]
    public void End_before_start_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => DateRange.Between(End, Start));
    }

    [Fact]
    public void Contains_treats_the_end_as_exclusive()
    {
        var range = DateRange.Between(Start, End);

        Assert.True(range.Contains(Start));
        Assert.False(range.Contains(End));
    }

    [Fact]
    public void Adjacent_ranges_do_not_overlap()
    {
        var morning = DateRange.Between(Start, End);
        var evening = DateRange.Between(End, End.AddHours(2));

        Assert.False(morning.Overlaps(evening));
    }

    [Fact]
    public void Duration_is_the_span_between_ends()
    {
        Assert.Equal(TimeSpan.FromHours(9), DateRange.Between(Start, End).Duration);
    }
}

public sealed class EmailAddressTests
{
    [Fact]
    public void A_valid_address_is_normalized_to_lowercase()
    {
        Assert.Equal("ops@factoryos.io", EmailAddress.Create("Ops@FactoryOS.io").Value);
    }

    [Fact]
    public void Addresses_are_compared_by_value()
    {
        Assert.Equal(EmailAddress.Create("a@b.co"), EmailAddress.Create("A@B.CO"));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing@tld")]
    [InlineData("@no-local.com")]
    public void An_invalid_address_is_rejected(string candidate)
    {
        Assert.Throws<ArgumentException>(() => EmailAddress.Create(candidate));
    }
}

public sealed class GeoLocationTests
{
    [Fact]
    public void Coordinates_out_of_range_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GeoLocation.Create(91d, 0d));
        Assert.Throws<ArgumentOutOfRangeException>(() => GeoLocation.Create(0d, 181d));
    }

    [Fact]
    public void A_valid_location_keeps_its_coordinates()
    {
        var location = GeoLocation.Create(41.0082d, 28.9784d);

        Assert.Equal(41.0082d, location.Latitude);
        Assert.Equal(28.9784d, location.Longitude);
    }
}
