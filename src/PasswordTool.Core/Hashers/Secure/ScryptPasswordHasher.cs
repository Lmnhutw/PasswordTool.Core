using System.Security.Cryptography;
using System.Text;
using PasswordTool.Core.Abstractions;
using PasswordTool.Core.Models;
using PasswordTool.Core.Options;
using PasswordTool.Core.Utilities;

namespace PasswordTool.Core.Hashers.Secure;

public sealed class ScryptPasswordHasher : IPasswordHasher
{
    public ScryptPasswordHasher(ScryptOptions options)
    {
        Options = options;
    }

    public string AlgorithmName => PasswordHasherNames.Scrypt;

    public bool IsRecommendedForPasswordStorage => true;

    public ScryptOptions Options { get; }

    public string HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(Options.SaltSizeBytes);
        var hash = Derive(password, salt, Options.Cost, Options.BlockSize, Options.Parallelization, Options.HashSizeBytes);

        return $"SCRYPT$v=1$N={Options.Cost}$r={Options.BlockSize}$p={Options.Parallelization}$salt={Convert.ToBase64String(salt)}$hash={Convert.ToBase64String(hash)}";
    }

    public bool VerifyPassword(string password, string storedHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        if (!TryRead(storedHash, out var cost, out var blockSize, out var parallelization, out var salt, out var expectedHash))
        {
            return false;
        }

        var actualHash = Derive(password, salt, cost, blockSize, parallelization, expectedHash.Length);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    public PasswordHashInfo InspectHash(string storedHash)
    {
        if (!TryRead(storedHash, out var cost, out var blockSize, out var parallelization, out var salt, out var hash))
        {
            return new PasswordHashInfo
            {
                AlgorithmName = "SCRYPT",
                IsSecureForPasswordStorage = false,
                Notes = "Invalid scrypt hash format."
            };
        }

        return new PasswordHashInfo
        {
            AlgorithmName = "SCRYPT",
            Version = 1,
            Salt = Convert.ToBase64String(salt),
            Hash = Convert.ToBase64String(hash),
            WorkFactor = cost,
            MemoryCost = blockSize,
            Parallelism = parallelization,
            HashSize = hash.Length,
            IsSecureForPasswordStorage = true,
            Notes = "Production-safe memory-hard password hash. N, r, p, salt, and hash are stored."
        };
    }

    private static byte[] Derive(string password, byte[] salt, int cost, int blockSize, int parallelization, int hashSize)
    {
        return ScryptKeyDerivation.DeriveKey(Encoding.UTF8.GetBytes(password), salt, cost, blockSize, parallelization, hashSize);
    }

    private static bool TryRead(
        string storedHash,
        out int cost,
        out int blockSize,
        out int parallelization,
        out byte[] salt,
        out byte[] hash)
    {
        cost = 0;
        blockSize = 0;
        parallelization = 0;
        salt = [];
        hash = [];

        return HashParser.TryParseKeyValueFormat(storedHash, out var algorithmName, out var values)
            && string.Equals(algorithmName, "SCRYPT", StringComparison.OrdinalIgnoreCase)
            && HashParser.TryGetInt(values, "N", out cost)
            && cost > 1
            && (cost & (cost - 1)) == 0
            && HashParser.TryGetInt(values, "r", out blockSize)
            && blockSize > 0
            && HashParser.TryGetInt(values, "p", out parallelization)
            && parallelization > 0
            && HashParser.TryGetBase64(values, "salt", out salt)
            && salt.Length > 0
            && HashParser.TryGetBase64(values, "hash", out hash)
            && hash.Length > 0;
    }
}
