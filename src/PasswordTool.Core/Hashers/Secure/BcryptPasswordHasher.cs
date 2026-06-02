using PasswordTool.Core.Abstractions;
using PasswordTool.Core.Models;
using PasswordTool.Core.Options;

namespace PasswordTool.Core.Hashers.Secure;

public sealed class BcryptPasswordHasher : IPasswordHasher
{
    public BcryptPasswordHasher(BcryptOptions options)
    {
        Options = options;
    }

    public string AlgorithmName => PasswordHasherNames.Bcrypt;

    public bool IsRecommendedForPasswordStorage => true;

    public BcryptOptions Options { get; }

    public string HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        return BCrypt.Net.BCrypt.HashPassword(password, Options.WorkFactor);
    }

    public bool VerifyPassword(string password, string storedHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, storedHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }

    public PasswordHashInfo InspectHash(string storedHash)
    {
        var parts = storedHash.Split('$', StringSplitOptions.None);
        if (parts.Length < 4 || !string.IsNullOrEmpty(parts[0]) || !parts[1].StartsWith('2') || !int.TryParse(parts[2], out var cost))
        {
            return new PasswordHashInfo
            {
                AlgorithmName = "bcrypt",
                IsSecureForPasswordStorage = false,
                Notes = "Invalid bcrypt hash format."
            };
        }

        var saltAndHash = parts[3];
        var salt = saltAndHash.Length >= 22 ? saltAndHash[..22] : saltAndHash;
        var hash = saltAndHash.Length > 22 ? saltAndHash[22..] : string.Empty;

        return new PasswordHashInfo
        {
            AlgorithmName = $"bcrypt ${parts[1]}",
            Salt = salt,
            Hash = hash,
            WorkFactor = cost,
            HashSize = hash.Length,
            IsSecureForPasswordStorage = true,
            Notes = "Production-safe when configured with an appropriate work factor."
        };
    }
}
