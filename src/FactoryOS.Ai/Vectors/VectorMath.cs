using FactoryOS.Domain.Results;

namespace FactoryOS.Ai.Vectors;

/// <summary>
/// Vector-space math over embedding vectors. Used by retrieval (RAG) to rank candidates by similarity to a
/// query vector. Operates on <see cref="IReadOnlyList{T}"/> of <see cref="float"/> so it is agnostic to how a
/// vector was produced.
/// </summary>
public static class VectorMath
{
    /// <summary>Computes the dot product of two equal-length vectors.</summary>
    /// <param name="left">The first vector.</param>
    /// <param name="right">The second vector.</param>
    /// <returns>The dot product, or a failure when the vectors differ in length.</returns>
    public static Result<double> Dot(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (left.Count != right.Count)
        {
            return Result.Failure<double>(DimensionMismatch(left.Count, right.Count));
        }

        double sum = 0d;
        for (var i = 0; i < left.Count; i++)
        {
            sum += (double)left[i] * right[i];
        }

        return Result.Success(sum);
    }

    /// <summary>Computes the Euclidean (L2) magnitude of a vector.</summary>
    /// <param name="vector">The vector.</param>
    /// <returns>The magnitude.</returns>
    public static double Magnitude(IReadOnlyList<float> vector)
    {
        ArgumentNullException.ThrowIfNull(vector);

        double sum = 0d;
        for (var i = 0; i < vector.Count; i++)
        {
            sum += (double)vector[i] * vector[i];
        }

        return Math.Sqrt(sum);
    }

    /// <summary>
    /// Computes the cosine similarity between two equal-length vectors, in <c>[-1, 1]</c> — higher means more
    /// similar. Fails when the vectors differ in length or either is a zero vector (undefined direction).
    /// </summary>
    /// <param name="left">The first vector.</param>
    /// <param name="right">The second vector.</param>
    /// <returns>The cosine similarity, or a failure when it is undefined.</returns>
    public static Result<double> CosineSimilarity(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (left.Count != right.Count)
        {
            return Result.Failure<double>(DimensionMismatch(left.Count, right.Count));
        }

        double dot = 0d;
        double leftSquared = 0d;
        double rightSquared = 0d;
        for (var i = 0; i < left.Count; i++)
        {
            double l = left[i];
            double r = right[i];
            dot += l * r;
            leftSquared += l * l;
            rightSquared += r * r;
        }

        var denominator = Math.Sqrt(leftSquared) * Math.Sqrt(rightSquared);
        if (denominator == 0d)
        {
            return Result.Failure<double>(Error.Failure(
                "Ai.Vector.ZeroMagnitude",
                "Cosine similarity is undefined for a zero vector."));
        }

        return Result.Success(dot / denominator);
    }

    private static Error DimensionMismatch(int leftCount, int rightCount) => Error.Failure(
        "Ai.Vector.DimensionMismatch",
        $"Vectors must have the same length; got {leftCount} and {rightCount}.");
}
