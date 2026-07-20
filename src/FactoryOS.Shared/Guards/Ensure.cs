using System.Diagnostics.CodeAnalysis;
using FactoryOS.Shared.Exceptions;

namespace FactoryOS.Shared.Guards;

/// <summary>
/// Invariant checks for domain and application rules. Unlike <see cref="Guard"/> (which validates method arguments
/// and throws <see cref="ArgumentException"/> family exceptions), <see cref="Ensure"/> asserts business invariants and
/// throws the domain exception family when they do not hold.
/// </summary>
public static class Ensure
{
    /// <summary>Ensures a business invariant holds.</summary>
    /// <param name="condition">The condition that must be <see langword="true"/>.</param>
    /// <param name="message">The message describing the violated rule when the condition is false.</param>
    /// <exception cref="BusinessException">Thrown when <paramref name="condition"/> is <see langword="false"/>.</exception>
    public static void That([DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition)
        {
            throw new BusinessException(message);
        }
    }

    /// <summary>Ensures a state precondition holds, otherwise reports a conflict.</summary>
    /// <param name="condition">The condition that must be <see langword="true"/>.</param>
    /// <param name="message">The message describing the conflict when the condition is false.</param>
    /// <exception cref="ConflictException">Thrown when <paramref name="condition"/> is <see langword="false"/>.</exception>
    public static void NoConflict([DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition)
        {
            throw new ConflictException(message);
        }
    }

    /// <summary>Ensures a resource was found (is not <see langword="null"/>) and returns it.</summary>
    /// <typeparam name="T">The resource type.</typeparam>
    /// <param name="value">The resource, or <see langword="null"/> when it does not exist.</param>
    /// <param name="message">The message describing what was not found.</param>
    /// <returns>The non-null resource.</returns>
    /// <exception cref="NotFoundException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
    public static T Found<T>([NotNull] T? value, string message)
        where T : class
    {
        if (value is null)
        {
            throw new NotFoundException(message);
        }

        return value;
    }
}

/// <summary>
/// Terminal throw helpers. Each is annotated with <see cref="DoesNotReturnAttribute"/>, so the compiler and analyzers
/// treat the call as an unconditional exit — enabling their use in expression positions and definite-assignment paths.
/// </summary>
public static class Throw
{
    /// <summary>Throws a <see cref="NotFoundException"/>.</summary>
    /// <param name="message">The message describing what was not found.</param>
    /// <exception cref="NotFoundException">Always thrown.</exception>
    [DoesNotReturn]
    public static void NotFound(string message) => throw new NotFoundException(message);

    /// <summary>Throws a <see cref="NotFoundException"/> as a typed expression.</summary>
    /// <typeparam name="T">The pretended return type at the call site.</typeparam>
    /// <param name="message">The message describing what was not found.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotFoundException">Always thrown.</exception>
    [DoesNotReturn]
    public static T NotFound<T>(string message) => throw new NotFoundException(message);

    /// <summary>Throws a <see cref="ConflictException"/>.</summary>
    /// <param name="message">The message describing the conflict.</param>
    /// <exception cref="ConflictException">Always thrown.</exception>
    [DoesNotReturn]
    public static void Conflict(string message) => throw new ConflictException(message);

    /// <summary>Throws a <see cref="BusinessException"/>.</summary>
    /// <param name="message">The message describing the violated rule.</param>
    /// <exception cref="BusinessException">Always thrown.</exception>
    [DoesNotReturn]
    public static void Business(string message) => throw new BusinessException(message);

    /// <summary>Throws a <see cref="ForbiddenException"/>.</summary>
    /// <param name="message">The message describing the failure.</param>
    /// <exception cref="ForbiddenException">Always thrown.</exception>
    [DoesNotReturn]
    public static void Forbidden(string message) => throw new ForbiddenException(message);
}
