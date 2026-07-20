using FactoryOS.Persistence.ValueConversion;
using FactoryOS.Shared.Identifiers;
using FactoryOS.Shared.Primitives;
using FactoryOS.Shared.ValueObjects;

namespace FactoryOS.Tests.Persistence;

/// <summary>A concrete enumeration used to exercise <see cref="EnumerationConverter{TEnumeration}"/>.</summary>
public sealed class SampleStatus : Enumeration
{
    public static readonly SampleStatus Idle = new(1, "Idle");
    public static readonly SampleStatus Running = new(2, "Running");

    private SampleStatus(int id, string name)
        : base(id, name)
    {
    }
}

public sealed class ValueConverterTests
{
    [Fact]
    public void Money_round_trips_through_its_string_form()
    {
        var converter = new MoneyConverter();
        var stored = (string)converter.ConvertToProvider(Money.Of(1234.56m, "usd"))!;
        var restored = (Money)converter.ConvertFromProvider(stored)!;

        Assert.Equal(1234.56m, restored.Amount);
        Assert.Equal("USD", restored.Currency);
    }

    [Fact]
    public void Percentage_round_trips_through_its_ratio()
    {
        var converter = new PercentageConverter();
        var stored = (decimal)converter.ConvertToProvider(Percentage.FromPercent(42m))!;
        var restored = (Percentage)converter.ConvertFromProvider(stored)!;

        Assert.Equal(0.42m, stored);
        Assert.Equal(42m, restored.Percent);
    }

    [Fact]
    public void DateRange_round_trips_through_its_string_form()
    {
        var converter = new DateRangeConverter();
        var range = DateRange.Between(
            new DateTimeOffset(2026, 07, 01, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 07, 31, 0, 0, 0, TimeSpan.Zero));

        var restored = (DateRange)converter.ConvertFromProvider(converter.ConvertToProvider(range)!)!;

        Assert.Equal(range, restored);
    }

    [Fact]
    public void Period_round_trips_through_its_string_form()
    {
        var converter = new PeriodConverter();
        var period = Period.ForMonth(2026, 7);

        var restored = (Period)converter.ConvertFromProvider(converter.ConvertToProvider(period)!)!;

        Assert.Equal(period, restored);
    }

    [Fact]
    public void Enumeration_round_trips_through_its_id()
    {
        var converter = new EnumerationConverter<SampleStatus>();
        var stored = (int)converter.ConvertToProvider(SampleStatus.Running)!;
        var restored = (SampleStatus)converter.ConvertFromProvider(stored)!;

        Assert.Equal(2, stored);
        Assert.Same(SampleStatus.Running, restored);
    }

    [Fact]
    public void A_strongly_typed_guid_id_round_trips()
    {
        var converter = new TenantIdConverter();
        var id = TenantId.New();

        var stored = (Guid)converter.ConvertToProvider(id)!;
        var restored = (TenantId)converter.ConvertFromProvider(stored)!;

        Assert.Equal(id.Value, stored);
        Assert.Equal(id, restored);
    }

    [Fact]
    public void A_strongly_typed_string_id_round_trips()
    {
        var converter = new CorrelationIdConverter();
        var id = CorrelationId.New();

        var stored = (string)converter.ConvertToProvider(id)!;
        var restored = (CorrelationId)converter.ConvertFromProvider(stored)!;

        Assert.Equal(id.Value, stored);
        Assert.Equal(id, restored);
    }

    [Fact]
    public void Utc_datetime_converter_reads_back_as_utc()
    {
        var converter = new UtcDateTimeConverter();
        var local = new DateTime(2026, 07, 20, 12, 0, 0, DateTimeKind.Local);

        var stored = (DateTime)converter.ConvertToProvider(local)!;
        var restored = (DateTime)converter.ConvertFromProvider(stored)!;

        Assert.Equal(DateTimeKind.Utc, restored.Kind);
    }
}
