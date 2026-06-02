using PasswordTool.Core.Abstractions;
using PasswordTool.Core.Models;

namespace PasswordTool.Core.Hashers;

public abstract class NotImplementedPasswordHasher : IPasswordHasher
{
    public abstract string AlgorithmName { get; }

    public virtual bool IsRecommendedForPasswordStorage => false;

    public string HashPassword(string password)
    {
        throw new NotImplementedException($"{AlgorithmName} hashing has not been implemented yet.");
    }

    public bool VerifyPassword(string password, string storedHash)
    {
        throw new NotImplementedException($"{AlgorithmName} verification has not been implemented yet.");
    }

    public PasswordHashInfo InspectHash(string storedHash)
    {
        throw new NotImplementedException($"{AlgorithmName} inspection has not been implemented yet.");
    }
}
