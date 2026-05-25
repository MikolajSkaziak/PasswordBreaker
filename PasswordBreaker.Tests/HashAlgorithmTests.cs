using PasswordBreaker.Shared.Cryptography;

namespace PasswordBreaker.Tests;

public class HashAlgorithmTests
{
    [Fact]
    public void Md5Hash_ShouldMatchExpectedHash()
    {
        var md5 = new Md5HashAlgorithm();
        string input = "test";
        string expectedHash = "098f6bcd4621d373cade4e832627b4f6"; // standard md5 for "test"

        var result = md5.ComputeHash(input);

        Assert.Equal(expectedHash, result);
        Assert.True(md5.VerifyHash(input, expectedHash));
    }

    [Fact]
    public void Sha256Hash_ShouldMatchExpectedHash()
    {
        var sha256 = new Sha256HashAlgorithm();
        string input = "test";
        string expectedHash = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08"; // standard sha256 for "test"

        var result = sha256.ComputeHash(input);

        Assert.Equal(expectedHash, result);
        Assert.True(sha256.VerifyHash(input, expectedHash));
    }
}
