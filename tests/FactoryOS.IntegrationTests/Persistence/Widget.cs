using FactoryOS.Domain.Abstractions;
using FactoryOS.Domain.Primitives;

namespace FactoryOS.IntegrationTests.Persistence;

/// <summary>A test aggregate exercising audit, soft-delete and concurrency persistence conventions.</summary>
public sealed class Widget : AggregateRoot<Guid>, IAuditableEntity, ISoftDeletable, IConcurrencyStamped
{
    private Widget(Guid id, string name)
        : base(id)
    {
        Name = name;
    }

    private Widget() => Name = string.Empty;

    public string Name { get; private set; }

    public DateTimeOffset CreatedOnUtc { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedOnUtc { get; private set; }

    public string? ModifiedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedOnUtc { get; private set; }

    public string? DeletedBy { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static Widget Create(Guid id, string name) => new(id, name);

    public void Rename(string name) => Name = name;

    public void ApplyCreated(DateTimeOffset whenUtc, string? actor)
    {
        CreatedOnUtc = whenUtc;
        CreatedBy = actor;
    }

    public void ApplyModified(DateTimeOffset whenUtc, string? actor)
    {
        ModifiedOnUtc = whenUtc;
        ModifiedBy = actor;
    }

    public void ApplyDeleted(DateTimeOffset whenUtc, string? actor)
    {
        IsDeleted = true;
        DeletedOnUtc = whenUtc;
        DeletedBy = actor;
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedOnUtc = null;
        DeletedBy = null;
    }

    public void StampConcurrency(Guid token) => ConcurrencyToken = token;
}
