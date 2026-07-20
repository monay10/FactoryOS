using FactoryOS.Ai.Vectors;

namespace FactoryOS.Tests.Ai;

public sealed class VectorMathTests
{
    [Fact]
    public void CosineSimilarity_of_identical_directions_is_one()
    {
        var result = VectorMath.CosineSimilarity([1f, 2f, 3f], [2f, 4f, 6f]);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Equal(1.0, result.Value, 6);
    }

    [Fact]
    public void CosineSimilarity_of_orthogonal_vectors_is_zero()
    {
        var result = VectorMath.CosineSimilarity([1f, 0f], [0f, 1f]);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Equal(0.0, result.Value, 6);
    }

    [Fact]
    public void CosineSimilarity_of_opposite_directions_is_minus_one()
    {
        var result = VectorMath.CosineSimilarity([1f, 1f], [-1f, -1f]);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Equal(-1.0, result.Value, 6);
    }

    [Fact]
    public void CosineSimilarity_fails_on_a_zero_vector()
    {
        var result = VectorMath.CosineSimilarity([0f, 0f], [1f, 1f]);

        Assert.True(result.IsFailure);
        Assert.Equal("Ai.Vector.ZeroMagnitude", result.Error.Code);
    }

    [Fact]
    public void CosineSimilarity_fails_on_a_dimension_mismatch()
    {
        var result = VectorMath.CosineSimilarity([1f, 2f], [1f, 2f, 3f]);

        Assert.True(result.IsFailure);
        Assert.Equal("Ai.Vector.DimensionMismatch", result.Error.Code);
    }

    [Fact]
    public void Dot_product_matches_the_hand_computed_value()
    {
        var result = VectorMath.Dot([1f, 2f, 3f], [4f, 5f, 6f]);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Equal(32.0, result.Value, 6); // 1*4 + 2*5 + 3*6
    }

    [Fact]
    public void Magnitude_matches_the_hand_computed_value()
    {
        var magnitude = VectorMath.Magnitude([3f, 4f]);

        Assert.Equal(5.0, magnitude, 6);
    }
}
