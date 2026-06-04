using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PasswordTool.Core.Services;

public sealed class EncryptionService
{
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public string EncryptString(string plaintext, byte[] key)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        return EncryptBytes(Encoding.UTF8.GetBytes(plaintext), key);
    }

    public string DecryptString(string encryptedPayloadJson, byte[] key)
    {
        var plaintext = DecryptBytes(encryptedPayloadJson, key);
        return Encoding.UTF8.GetString(plaintext);
    }

    public string EncryptObject<T>(T value, byte[] key)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        return EncryptString(json, key);
    }

    public T DecryptObject<T>(string encryptedPayloadJson, byte[] key)
    {
        var json = DecryptString(encryptedPayloadJson, key);
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new InvalidOperationException("The encrypted payload did not contain valid JSON.");
    }

    private static string EncryptBytes(byte[] plaintext, byte[] key)
    {
        ValidateKey(key);

        var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSizeBytes];

        using var aes = new AesGcm(key, TagSizeBytes);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var payload = new EncryptedPayload
        {
            Version = 1,
            Algorithm = "AES-256-GCM",
            NonceBase64 = Convert.ToBase64String(nonce),
            TagBase64 = Convert.ToBase64String(tag),
            CipherTextBase64 = Convert.ToBase64String(ciphertext)
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static byte[] DecryptBytes(string encryptedPayloadJson, byte[] key)
    {
        ValidateKey(key);

        var payload = JsonSerializer.Deserialize<EncryptedPayload>(encryptedPayloadJson, JsonOptions)
            ?? throw new CryptographicException("The encrypted payload is empty or invalid.");

        if (payload.Version != 1 || !string.Equals(payload.Algorithm, "AES-256-GCM", StringComparison.Ordinal))
        {
            throw new CryptographicException("The encrypted payload format is not supported.");
        }

        var nonce = Convert.FromBase64String(payload.NonceBase64);
        var tag = Convert.FromBase64String(payload.TagBase64);
        var ciphertext = Convert.FromBase64String(payload.CipherTextBase64);

        if (nonce.Length != NonceSizeBytes || tag.Length != TagSizeBytes)
        {
            throw new CryptographicException("The encrypted payload is malformed.");
        }

        var plaintext = new byte[ciphertext.Length];
        using var aes = new AesGcm(key, TagSizeBytes);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        return plaintext;
    }

    private static void ValidateKey(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (key.Length != 32)
        {
            throw new ArgumentException("AES-256-GCM requires a 32-byte key.", nameof(key));
        }
    }

    private sealed class EncryptedPayload
    {
        public int Version { get; set; }

        public string Algorithm { get; set; } = string.Empty;

        public string NonceBase64 { get; set; } = string.Empty;

        public string TagBase64 { get; set; } = string.Empty;

        public string CipherTextBase64 { get; set; } = string.Empty;
    }
}
