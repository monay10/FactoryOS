using FactoryOS.Domain.Abstractions;

namespace FactoryOS.Domain.Time;

/// <summary>Default <see cref="IDateTimeProvider"/> backed by the system clock via <see cref="DateTimeOffset.UtcNow"/>.</summary>
public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
