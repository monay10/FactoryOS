using FactoryOS.Domain.Results;

namespace FactoryOS.Tests.Results;

public sealed class ResultTests
{
    [Fact]
    public void Success_result_has_no_error()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Failure_result_carries_the_error()
    {
        var error = Error.Validation("code", "description");

        var result = Result.Failure(error);

        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void Success_result_of_value_exposes_the_value()
    {
        var result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Accessing_the_value_of_a_failure_throws()
    {
        var result = Result.Failure<int>(Error.Failure("code", "boom"));

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void A_value_is_implicitly_converted_to_a_success_result()
    {
        Result<string> result = "ready";

        Assert.True(result.IsSuccess);
        Assert.Equal("ready", result.Value);
    }

    [Fact]
    public void A_successful_result_cannot_be_constructed_with_an_error()
    {
        Assert.Throws<InvalidOperationException>(() => Result.Failure(Error.None));
    }
}
