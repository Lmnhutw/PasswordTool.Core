using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using PasswordTool.Core.Abstractions;
using PasswordTool.Core.Models;
using PasswordTool.Core.Options;
using PasswordTool.Core.Utilities;

namespace PasswordTool.Core.Hashers.Secure;

public sealed class Argon2idPasswordHasher : IPasswordHasher
{
    public Argon2idPasswordHasher(Argon2idOptions options)
    {
        Options = options;
    }

    public string AlgorithmName => PasswordHasherNames.Argon2id;

    public bool IsRecommendedForPasswordStorage => true;

    public Argon2idOptions Options { get; }

    public string HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(Options.SaltSizeBytes);
        var hash = Derive(password, salt, Options.MemoryCost, Options.Iterations, Options.Parallelism, Options.HashSizeBytes);

        return $"ARGON2ID$v=1$m={Options.MemoryCost}$t={Options.Iterations}$p={Options.Parallelism}$salt={Convert.ToBase64String(salt)}$hash={Convert.ToBase64String(hash)}";
    }

    public bool VerifyPassword(string password, string storedHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        if (!TryRead(storedHash, out var memoryCost, out var iterations, out var parallelism, out var salt, out var expectedHash))
        {
            return false;
        }

        var actualHash = Derive(password, salt, memoryCost, iterations, parallelism, expectedHash.Length);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    public PasswordHashInfo InspectHash(string storedHash)
    {
        if (!TryRead(storedHash, out var memoryCost, out var iterations, out var parallelism, out var salt, out var hash))
        {
            return new PasswordHashInfo
            {
                AlgorithmName = "ARGON2ID",
                IsSecureForPasswordStorage = false,
                Notes = "Invalid Argon2id hash format or Base64 value."
            };
        }

        return new PasswordHashInfo
        {
            AlgorithmName = "ARGON2ID",
            Version = 1,
            Salt = Convert.ToBase64String(salt),
            Hash = Convert.ToBase64String(hash),
            Iterations = iterations,
            MemoryCost = memoryCost,
            Parallelism = parallelism,
            HashSize = hash.Length,
            IsSecureForPasswordStorage = true,
            Notes = "Production-safe memory-hard password hash. Parameters are stored for verification."
        };
    }

    private static byte[] Derive(string password, byte[] salt, int memoryCost, int iterations, int parallelism, int hashSize)
    {
        var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = memoryCost,
            Iterations = iterations,
            DegreeOfParallelism = parallelism
        };

        return argon2.GetBytes(hashSize);
    }

    private static bool TryRead(
        string storedHash,
        out int memoryCost,
        out int iterations,
        out int parallelism,
        out byte[] salt,
        out byte[] hash)
    {
        memoryCost = 0;
        iterations = 0;
        parallelism = 0;
        salt = [];
        hash = [];

        return HashParser.TryParseKeyValueFormat(storedHash, out var algorithmName, out var values)
            && string.Equals(algorithmName, "ARGON2ID", StringComparison.OrdinalIgnoreCase)
            && HashParser.TryGetInt(values, "m", out memoryCost)
            && memoryCost > 0
            && HashParser.TryGetInt(values, "t", out iterations)
            && iterations > 0
            && HashParser.TryGetInt(values, "p", out parallelism)
            && parallelism > 0
            && HashParser.TryGetBase64(values, "salt", out salt)
            && salt.Length > 0
            && HashParser.TryGetBase64(values, "hash", out hash)
            && hash.Length > 0;
    }
}
