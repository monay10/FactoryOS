using System.Globalization;

namespace FactoryOS.Plugins.Workflow.Engine.Domain;

/// <summary>
/// A monotonically increasing workflow definition version. A definition key plus its version identifies a
/// specific, immutable process graph; publishing a change increments the version so running instances keep
/// executing the version they started on.
/// </summary>
/// <param name="Value">The version number (1 or greater).</param>
public readonly record struct WorkflowVersion(int Value) : IComparable<WorkflowVersion>, IComparable
{
    /// <summary>The first version of a definition.</summary>
    public static readonly WorkflowVersion Initial = new(1);

    /// <summary>Returns the next version.</summary>
    /// <returns>A version one greater than this one.</returns>
    public WorkflowVersion Next() => new(Value + 1);

    /// <inheritdoc />
    public int CompareTo(WorkflowVersion other) => Value.CompareTo(other.Value);

    /// <inheritdoc />
    public int CompareTo(object? obj) => obj switch
    {
        null => 1,
        WorkflowVersion other => CompareTo(other),
        _ => throw new ArgumentException($"Object must be of type {nameof(WorkflowVersion)}.", nameof(obj)),
    };

    /// <summary>Determines whether one version precedes another.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is lower.</returns>
    public static bool operator <(WorkflowVersion left, WorkflowVersion right) => left.Value < right.Value;

    /// <summary>Determines whether one version precedes or equals another.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is lower or equal.</returns>
    public static bool operator <=(WorkflowVersion left, WorkflowVersion right) => left.Value <= right.Value;

    /// <summary>Determines whether one version follows another.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is higher.</returns>
    public static bool operator >(WorkflowVersion left, WorkflowVersion right) => left.Value > right.Value;

    /// <summary>Determines whether one version follows or equals another.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is higher or equal.</returns>
    public static bool operator >=(WorkflowVersion left, WorkflowVersion right) => left.Value >= right.Value;

    /// <inheritdoc />
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"v{Value}");
}
