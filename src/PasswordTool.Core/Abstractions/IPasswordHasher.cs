namespace PasswordTool.Core.Abstractions;

public interface IPasswordHasher
{
    string AlgorithmName { get; }

    bool IsRecommendedForPasswordStorage { get; }

    string HashPassword(string password);

    bool VerifyPassword(string password, string storedHash);

    Models.PasswordHashInfo InspectHash(string storedHash);
}
