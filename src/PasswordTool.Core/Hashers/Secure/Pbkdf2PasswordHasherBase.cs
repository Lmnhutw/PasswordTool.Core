using System.Security.Cryptography;
using System.Text;
using PasswordTool.Core.Abstractions;
using PasswordTool.Core.Models;
using PasswordTool.Core.Options;
using PasswordTool.Core.Utilities;

namespace PasswordTool.Core.Hashers.Secure;

public abstract class Pbkdf2PasswordHasherBase : IPasswordHasher
{
    protected Pbkdf2PasswordHasherBase(Pbkdf2Options options, HashAlgorithmName hashAlgorithmName, string formatName)
    {
        Options = options;
        HashAlgorithmName = hashAlgorithmName;
        FormatName = formatName;
    }

    public abstract string AlgorithmName { get; }

    public bool IsRecommendedForPasswordStorage => true;

    protected Pbkdf2Options Options { get; }

    protected HashAlgorithmName HashAlgorithmName { get; }

    protected string FormatName { get; }

    public string HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(Options.SaltSizeBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Options.Iterations,
            HashAlgorithmName,
            Options.HashSizeBytes);

        return $"{FormatName}$v=1$iter={Options.Iterations}$salt={Convert.ToBase64String(salt)}$hash={Convert.ToBase64String(hash)}";
    }

    public bool VerifyPassword(string password, string storedHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        if (!TryRead(storedHash, out var iterations, out var salt, out var expectedHash))
        {
            return false;
        }

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            HashAlgorithmName,
            expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    public PasswordHashInfo InspectHash(string storedHash)
    {
        if (!TryRead(storedHash, out var iterations, out var salt, out var hash))
        {
            return InvalidInfo("Invalid PBKDF2 hash format or Base64 value.");
        }

        return new PasswordHashInfo
        {
            AlgorithmName = FormatName,
            Version = 1,
            Salt = Convert.ToBase64String(salt),
            Hash = Convert.ToBase64String(hash),
            Iterations = iterations,
            HashSize = hash.Length,
            IsSecureForPasswordStorage = true,
            Notes = "Production-safe when configured with a strong iteration count and random salt."
        };
    }

    protected PasswordHashInfo InvalidInfo(string notes)
    {
        return new PasswordHashInfo
        {
            AlgorithmName = FormatName,
            IsSecureForPasswordStorage = false,
            Notes = notes
        };
    }

    private bool TryRead(string storedHash, out int iterations, out byte[] salt, out byte[] hash)
    {
        iterations = 0;
        salt = [];
        hash = [];

        return HashParser.TryParseKeyValueFormat(storedHash, out var algorithmName, out var values)
            && string.Equals(algorithmName, FormatName, StringComparison.OrdinalIgnoreCase)
            && HashParser.TryGetInt(values, "iter", out iterations)
            && iterations > 0
            && HashParser.TryGetBase64(values, "salt", out salt)
            && salt.Length > 0
            && HashParser.TryGetBase64(values, "hash", out hash)
            && hash.Length > 0;
    }
}
