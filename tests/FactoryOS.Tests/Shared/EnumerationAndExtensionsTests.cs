using FactoryOS.Shared.Extensions;
using FactoryOS.Shared.Identifiers;
using FactoryOS.Shared.Primitives;

namespace FactoryOS.Tests.Shared;

public sealed class EnumerationTests
{
    private sealed class Shift : Enumeration
    {
        public static readonly Shift Morning = new(1, "Morning");
        public static readonly Shift Evening = new(2, "Evening");
        public static readonly Shift Night = new(3, "Night");

        private Shift(int id, string name)
            : base(id, name)
        {
        }
    }

    [Fact]
    public void GetAll_returns_every_declared_member()
    {
        Assert.Equal(3, Enumeration.GetAll<Shift>().Count);
    }

    [Fact]
    public void FromId_resolves_a_member_by_id()
    {
        Assert.Equal(Shift.Evening, Enumeration.FromId<Shift>(2));
    }

    [Fact]
    public void An_unknown_id_throws()
    {
        Assert.Throws<InvalidOperationException>(() => Enumeration.FromId<Shift>(99));
    }

    [Fact]
    public void Members_are_equal_by_id_and_type()
    {
        Assert.Equal(Shift.Morning, Enumeration.FromId<Shift>(1));
        Assert.NotEqual(Shift.Morning, Shift.Night);
    }
}

public sealed class StronglyTypedIdTests
{
    [Fact]
    public void Ids_with_the_same_value_are_equal()
    {
        var value = Guid.NewGuid();

        Assert.Equal(new TenantId(value), new TenantId(value));
    }

    [Fact]
    public void New_ids_are_distinct()
    {
        Assert.NotEqual(MachineId.New(), MachineId.New());
    }
}

public sealed class ExtensionsTests
{
    [Fact]
    public void HasValue_detects_meaningful_strings()
    {
        Assert.True("x".HasValue());
        Assert.False("   ".HasValue());
        Assert.False(((string?)null).HasValue());
    }

    [Fact]
    public void Truncate_caps_the_length()
    {
        Assert.Equal("abc", "abcdef".Truncate(3));
        Assert.Equal("ab", "ab".Truncate(5));
    }

    [Fact]
    public void WhereNotNull_drops_nulls()
    {
        string?[] input = ["a", null, "b"];

        var result = input.WhereNotNull().ToArray();

        Assert.Equal(2, result.Length);
        Assert.Equal("a", result[0]);
        Assert.Equal("b", result[1]);
    }

    [Fact]
    public void IsNullOrEmpty_detects_empty_sequences()
    {
        var one = new List<int> { 1 };

        Assert.True(Array.Empty<int>().IsNullOrEmpty());
        Assert.False(one.IsNullOrEmpty());
    }

    [Fact]
    public void Json_round_trips_a_value()
    {
        var source = new List<int> { 1, 2, 3 };

        var restored = source.ToJson().FromJson<List<int>>();

        Assert.NotNull(restored);
        Assert.Equal(source, restored);
    }
}
