using System.Security.Cryptography;
using PasswordTool.Core.Models;

namespace PasswordTool.Core.Services;

public sealed class MasterPasswordService
{
    public const int SaltSizeBytes = 32;
    public const int DefaultKeySizeBytes = 32;
    public const int DefaultPbkdf2Iterations = 600_000;

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
            Version = 1,
            KdfAlgorithm = "PBKDF2-HMACSHA256",
            KdfIterations = DefaultPbkdf2Iterations,
            KeySizeBytes = DefaultKeySizeBytes,
            SaltBase64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(SaltSizeBytes)),
            CreatedAt = now,
            UpdatedAt = now
        };

        var key = DeriveKey(masterPassword, config);
        try
        {
            config.EncryptedTotpSecret = encryptionService.EncryptString(totpSecretBase32, key);
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

        if (!string.Equals(config.KdfAlgorithm, "PBKDF2-HMACSHA256", StringComparison.Ordinal))
        {
            throw new NotSupportedException($"Unsupported key derivation algorithm: {config.KdfAlgorithm}");
        }

        if (config.KdfIterations < 100_000)
        {
            throw new InvalidOperationException("The configured PBKDF2 iteration count is too low.");
        }

        var salt = Convert.FromBase64String(config.SaltBase64);
        if (salt.Length < SaltSizeBytes)
        {
            throw new InvalidOperationException("The configured salt is too short.");
        }

        return Rfc2898DeriveBytes.Pbkdf2(
            masterPassword,
            salt,
            config.KdfIterations,
            HashAlgorithmName.SHA256,
            config.KeySizeBytes);
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
