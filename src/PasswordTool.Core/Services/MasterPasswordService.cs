using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using PasswordTool.Core.Models;

namespace PasswordTool.Core.Services;

public sealed class MasterPasswordService
{
    public const int SaltSizeBytes = 32;
    public const int DefaultKeySizeBytes = 32;
    public const int DefaultPbkdf2Iterations = 600_000;
    public const int DefaultArgon2Iterations = 3;
    public const int DefaultArgon2MemorySizeKb = 65_536;
    public const int DefaultArgon2Parallelism = 2;
    public const string Pbkdf2Algorithm = "PBKDF2-HMACSHA256";
    public const string Argon2idAlgorithm = "ARGON2ID";

    private readonly EncryptionService encryptionService;

    public MasterPasswordService()
        : this(new EncryptionService())
    {
    }

    public MasterPasswordService(EncryptionService encryptionService)
    {
        this.encryptionService = encryptionService;
    }

    public AppConfig CreateConfig(string masterPassword, string totpSecretBase32)
    {
        ValidateNewMasterPassword(masterPassword);

        var now = DateTimeOffset.UtcNow;
        var config = new AppConfig
        {
            Version = 2,
            KdfAlgorithm = Argon2idAlgorithm,
            KdfIterations = DefaultArgon2Iterations,
            KdfMemorySizeKb = DefaultArgon2MemorySizeKb,
            KdfParallelism = DefaultArgon2Parallelism,
            KeySizeBytes = DefaultKeySizeBytes,
            SaltBase64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(SaltSizeBytes)),
            CreatedAt = now,
            UpdatedAt = now
        };

        var key = DeriveKey(masterPassword, config);
        try
        {
            config.EncryptedTotpSecret = string.IsNullOrWhiteSpace(totpSecretBase32)
                ? string.Empty
                : encryptionService.EncryptString(totpSecretBase32, key);
            return config;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public byte[] DeriveKey(string masterPassword, AppConfig config)
    {
        if (string.IsNullOrWhiteSpace(masterPassword))
        {
            throw new ArgumentException("Master Password is required.", nameof(masterPassword));
        }

        var salt = Convert.FromBase64String(config.SaltBase64);
        if (salt.Length < SaltSizeBytes)
        {
            throw new InvalidOperationException("The configured salt is too short.");
        }

        if (config.KeySizeBytes != DefaultKeySizeBytes)
        {
            throw new InvalidOperationException("The configured key size is not supported.");
        }

        if (string.Equals(config.KdfAlgorithm, Pbkdf2Algorithm, StringComparison.Ordinal))
        {
            if (config.KdfIterations < 100_000)
            {
                throw new InvalidOperationException("The configured PBKDF2 iteration count is too low.");
            }

            return Rfc2898DeriveBytes.Pbkdf2(
                masterPassword,
                salt,
                config.KdfIterations,
                HashAlgorithmName.SHA256,
                config.KeySizeBytes);
        }

        if (!string.Equals(config.KdfAlgorithm, Argon2idAlgorithm, StringComparison.Ordinal))
        {
            throw new NotSupportedException($"Unsupported key derivation algorithm: {config.KdfAlgorithm}");
        }

        if (config.KdfIterations is < 2 or > 10
            || config.KdfMemorySizeKb is < 32_768 or > 1_048_576
            || config.KdfParallelism is < 1 or > 16)
        {
            throw new InvalidOperationException("The configured Argon2id parameters are outside the supported range.");
        }

        var passwordBytes = Encoding.UTF8.GetBytes(masterPassword);
        try
        {
            using var argon2 = new Argon2id(passwordBytes)
            {
                Salt = salt,
                Iterations = config.KdfIterations,
                MemorySize = config.KdfMemorySizeKb,
                DegreeOfParallelism = config.KdfParallelism
            };
            return argon2.GetBytes(config.KeySizeBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    public static bool NeedsKdfUpgrade(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return !string.Equals(config.KdfAlgorithm, Argon2idAlgorithm, StringComparison.Ordinal)
            || config.KdfIterations < DefaultArgon2Iterations
            || config.KdfMemorySizeKb < DefaultArgon2MemorySizeKb
            || config.KdfParallelism < DefaultArgon2Parallelism;
    }

    public bool TryUnlockConfig(
        string masterPassword,
        AppConfig config,
        out byte[] encryptionKey,
        out string totpSecretBase32)
    {
        encryptionKey = [];
        totpSecretBase32 = string.Empty;

        try
        {
            encryptionKey = DeriveKey(masterPassword, config);
            if (!string.IsNullOrWhiteSpace(config.EncryptedTotpSecret))
            {
                totpSecretBase32 = encryptionService.DecryptString(config.EncryptedTotpSecret, encryptionKey);
            }

            return true;
        }
        catch (Exception ex) when (ex is ArgumentException
            or FormatException
            or CryptographicException
            or InvalidOperationException
            or NotSupportedException)
        {
            if (encryptionKey.Length > 0)
            {
                CryptographicOperations.ZeroMemory(encryptionKey);
                encryptionKey = [];
            }

            totpSecretBase32 = string.Empty;
            return false;
        }
    }

    public static void ValidateNewMasterPassword(string masterPassword)
    {
        if (string.IsNullOrWhiteSpace(masterPassword))
        {
            throw new ArgumentException("Master Password is required.", nameof(masterPassword));
        }

        if (masterPassword.Length < 12)
        {
            throw new ArgumentException("Use a Master Password with at least 12 characters.", nameof(masterPassword));
        }
    }
}
