using System.Security.Cryptography;
using PasswordTool.Core.Models;

namespace PasswordTool.Core.Services;

public sealed class RecoveryKeyService
{
    public const string Context = "PasswordTool/v4/recovery-dek";
    private readonly EncryptionService encryption = new();

    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        try
        {
            var hex = Convert.ToHexString(bytes);
            return "RK1-" + string.Join('-', Enumerable.Range(0, 8).Select(i => hex.Substring(i * 8, 8)));
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private static byte[] Decode(string value)
    {
        if (value is null || value.Length != 75 || !value.StartsWith("RK1-", StringComparison.Ordinal))
            throw new FormatException("Unsupported Recovery Key format.");
        var groups = value[4..].Split('-');
        if (groups.Length != 8 || groups.Any(g => g.Length != 8))
            throw new FormatException("Invalid Recovery Key format.");
        return Convert.FromHexString(string.Concat(groups));
    }

    public RecoveryKeySlot Wrap(string recoveryKey, byte[] vaultKey)
    {
        var key = Decode(recoveryKey);
        try { return new RecoveryKeySlot { WrappedVaultKey = encryption.EncryptKey(vaultKey, key, Context) }; }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    public byte[] Unwrap(string recoveryKey, RecoveryKeySlot slot)
    {
        if (slot.Version != 1 || slot.WrapAlgorithm != "AES-256-GCM")
            throw new NotSupportedException("Unsupported Recovery Key slot.");
        var key = Decode(recoveryKey);
        try
        {
            var dek = encryption.DecryptKey(slot.WrappedVaultKey, key, Context);
            if (dek.Length == 32) return dek;
            CryptographicOperations.ZeroMemory(dek);
            throw new CryptographicException("Invalid vault key.");
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }
}
