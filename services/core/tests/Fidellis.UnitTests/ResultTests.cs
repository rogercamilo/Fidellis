using Fidellis.SharedKernel;
using Xunit;

namespace Fidellis.UnitTests;

public class ResultTests
{
    [Fact]
    public void Success_has_no_error()
    {
        var result = Result.Success();
        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_carries_error_message()
    {
        var result = Result.Failure("boom");
        Assert.True(result.IsFailure);
        Assert.Equal("boom", result.Error);
    }

    [Fact]
    public void Success_with_value_exposes_value()
    {
        var result = Result.Success(42);
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Failure_value_access_throws()
    {
        var result = Result.Failure<int>("nope");
        Assert.Throws<InvalidOperationException>(() => _ = result.Value);
    }
}
