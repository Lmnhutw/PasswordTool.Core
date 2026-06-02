using PasswordTool.Core.Abstractions;

namespace PasswordTool.Core.Hashers;

public abstract class NotImplementedPasswordHasher : IPasswordHasher
{
    public abstract string AlgorithmName { get; }

    public string HashPassword(string password)
    {
        throw new NotImplementedException($"{AlgorithmName} hashing has not been implemented yet.");
    }

    public bool VerifyPassword(string password, string storedHash)
    {
        throw new NotImplementedException($"{AlgorithmName} verification has not been implemented yet.");
    }
}
