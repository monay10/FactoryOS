using FactoryOS.Domain.Results;

namespace FactoryOS.Tests.Results;

public sealed class ErrorTests
{
    [Fact]
    public void Factory_methods_set_the_expected_error_type()
    {
        Assert.Equal(ErrorType.Failure, Error.Failure("c", "d").Type);
        Assert.Equal(ErrorType.Validation, Error.Validation("c", "d").Type);
        Assert.Equal(ErrorType.NotFound, Error.NotFound("c", "d").Type);
        Assert.Equal(ErrorType.Conflict, Error.Conflict("c", "d").Type);
    }

    [Fact]
    public void Factory_methods_preserve_code_and_description()
    {
        var error = Error.Validation("VAL001", "Value is required.");

        Assert.Equal("VAL001", error.Code);
        Assert.Equal("Value is required.", error.Description);
    }

    [Fact]
    public void Errors_with_equal_components_are_equal()
    {
        Assert.Equal(Error.NotFound("X", "Y"), Error.NotFound("X", "Y"));
    }

    [Fact]
    public void None_represents_the_absence_of_an_error()
    {
        Assert.Equal(string.Empty, Error.None.Code);
        Assert.Equal(string.Empty, Error.None.Description);
    }
}
