using AilabTemplate.Api.Security;

namespace AilabTemplate.Api.Tests;

public sealed class BearerTokenFilterTests
{
    [Theory]
    [InlineData("abc", "abc", true)]
    [InlineData("abc", "abd", false)]
    [InlineData("abc", "abcd", false)]
    [InlineData("", "abc", false)]
    [InlineData("ABC", "abc", false)]
    public void TokensMatch_IsExactAndCaseSensitive(string provided, string expected, bool match) =>
        Assert.Equal(match, BearerTokenFilter.TokensMatch(provided, expected));
}
