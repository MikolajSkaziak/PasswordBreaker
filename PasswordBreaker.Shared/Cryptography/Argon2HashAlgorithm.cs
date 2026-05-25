using System.Text;
using Konscious.Security.Cryptography;

namespace PasswordBreaker.Shared.Cryptography;

public class Argon2HashAlgorithm : IHashAlgorithm
{
    // For brute-force to even be somewhat feasible, we assume fixed parameters
    // In a real scenario, salt and parameters are extracted from the hash string.
    // For simplicity, we use a fixed salt and low cost if we are brute-forcing it.
    private readonly byte[] _salt = Encoding.UTF8.GetBytes("fixed_salt_for_demo");

    public string ComputeHash(string input)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(input))
        {
            DegreeOfParallelism = 1,
            MemorySize = 1024, // 1 MB
            Iterations = 1,
            Salt = _salt
        };

        var hashBytes = argon2.GetBytes(16);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public bool VerifyHash(string input, string targetHash)
    {
        return ComputeHash(input) == targetHash.ToLowerInvariant();
    }
}
