using System.Security.Cryptography;
using System.Text;

namespace PasswordBreaker.Shared.Cryptography;

public class Sha256HashAlgorithm : IHashAlgorithm
{
    public string ComputeHash(string input)
    {
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = SHA256.HashData(inputBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public bool VerifyHash(string input, string targetHash)
    {
        return ComputeHash(input) == targetHash.ToLowerInvariant();
    }
}
