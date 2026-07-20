using FactoryOS.Domain.Primitives;

namespace FactoryOS.Tests.Primitives;

public sealed class EntityTests
{
    private sealed class Sample : Entity<Guid>
    {
        public Sample(Guid id)
            : base(id)
        {
        }
    }

    private sealed class OtherSample : Entity<Guid>
    {
        public OtherSample(Guid id)
            : base(id)
        {
        }
    }

    [Fact]
    public void Entities_with_the_same_id_are_equal()
    {
        var id = Guid.NewGuid();
        var first = new Sample(id);
        var second = new Sample(id);

        Assert.Equal(first, second);
        Assert.True(first == second);
        Assert.False(first != second);
    }

    [Fact]
    public void Entities_with_different_ids_are_not_equal()
    {
        var first = new Sample(Guid.NewGuid());
        var second = new Sample(Guid.NewGuid());

        Assert.NotEqual(first, second);
        Assert.True(first != second);
    }

    [Fact]
    public void Entities_of_different_types_with_the_same_id_are_not_equal()
    {
        var id = Guid.NewGuid();
        var entity = new Sample(id);
        var other = new OtherSample(id);

        Assert.False(entity.Equals(other));
    }

    [Fact]
    public void Entities_with_the_same_id_share_a_hash_code()
    {
        var id = Guid.NewGuid();

        Assert.Equal(new Sample(id).GetHashCode(), new Sample(id).GetHashCode());
    }

    [Fact]
    public void Entity_is_not_equal_to_null()
    {
        var entity = new Sample(Guid.NewGuid());

        Assert.False(entity.Equals(null));
        Assert.True(entity != null);
    }

    [Fact]
    public void Constructing_an_entity_with_a_null_id_throws()
    {
        Assert.Throws<ArgumentNullException>(() => new StringEntity(null!));
    }

    private sealed class StringEntity : Entity<string>
    {
        public StringEntity(string id)
            : base(id)
        {
        }
    }
}
