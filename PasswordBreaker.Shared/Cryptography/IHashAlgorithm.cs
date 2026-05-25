namespace PasswordBreaker.Shared.Cryptography;

public interface IHashAlgorithm
{
    string ComputeHash(string input);
    bool VerifyHash(string input, string targetHash);
}
