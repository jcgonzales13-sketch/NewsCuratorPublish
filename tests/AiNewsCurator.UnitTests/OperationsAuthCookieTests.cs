using AiNewsCurator.Api.Operations;

namespace AiNewsCurator.UnitTests;

public sealed class OperationsAuthCookieTests
{
    [Fact]
    public void CreateValue_Should_Return_Stable_Hash()
    {
        var first = OperationsAuthCookie.CreateValue("secret-key");
        var second = OperationsAuthCookie.CreateValue("secret-key");

        Assert.Equal(first, second);
        Assert.NotEqual(first, OperationsAuthCookie.CreateValue("other-key"));
    }

    [Fact]
    public void Matches_Should_Return_True_Only_For_Expected_Api_Key()
    {
        var cookieValue = OperationsAuthCookie.CreateValue("secret-key");

        Assert.True(OperationsAuthCookie.Matches(cookieValue, "secret-key"));
        Assert.False(OperationsAuthCookie.Matches(cookieValue, "wrong-key"));
        Assert.False(OperationsAuthCookie.Matches(null, "secret-key"));
        Assert.False(OperationsAuthCookie.Matches("   ", "secret-key"));
    }
}
