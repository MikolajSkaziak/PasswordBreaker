using PasswordBreaker.Shared.Utils;

namespace PasswordBreaker.Tests;

public class BruteForceGeneratorTests
{
    [Theory]
    [InlineData(0, "a")]
    [InlineData(1, "b")]
    [InlineData(2, "c")]
    [InlineData(3, "aa")]
    [InlineData(4, "ab")]
    [InlineData(5, "ac")]
    [InlineData(6, "ba")]
    [InlineData(7, "bb")]
    [InlineData(8, "bc")]
    [InlineData(9, "ca")]
    [InlineData(10, "cb")]
    [InlineData(11, "cc")]
    [InlineData(12, "aaa")]
    public void GenerateStringAtIndex_ShouldReturnCorrectString(long index, string expected)
    {
        string alphabet = "abc";
        int maxLength = 3;

        string result = BruteForceGenerator.GenerateStringAtIndex(index, alphabet, maxLength);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void GenerateStringAtIndex_ExceedingMaxLength_ShouldReturnEmpty()
    {
        string alphabet = "ab";
        // max length 1 means indices 0, 1 are "a", "b". Index 2 is "aa" which is length 2.
        string result = BruteForceGenerator.GenerateStringAtIndex(2, alphabet, 1);

        Assert.Equal(string.Empty, result);
    }
}
