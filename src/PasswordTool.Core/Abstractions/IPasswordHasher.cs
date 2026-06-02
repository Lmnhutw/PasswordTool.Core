namespace PasswordTool.Core.Abstractions;

public interface IPasswordHasher
{
    string AlgorithmName { get; }

    string HashPassword(string password);

    bool VerifyPassword(string password, string storedHash);
}
