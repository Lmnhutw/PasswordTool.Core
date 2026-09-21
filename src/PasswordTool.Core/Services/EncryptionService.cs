using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PasswordTool.Core.Services;

public sealed class EncryptionService
{
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    public string EncryptString(string plaintext, byte[] key) => EncryptStringCore(plaintext, key, null);
    public string EncryptString(string plaintext, byte[] key, string context) => EncryptStringCore(plaintext, key, ValidateContext(context));

    public string DecryptString(string encryptedPayloadJson, byte[] key) => DecryptStringCore(encryptedPayloadJson, key, null, 1);
    public string DecryptString(string encryptedPayloadJson, byte[] key, string context) => DecryptStringCore(encryptedPayloadJson, key, ValidateContext(context), 2);

    public string EncryptObject<T>(T value, byte[] key) => EncryptString(JsonSerializer.Serialize(value, JsonOptions), key);
    public string EncryptObject<T>(T value, byte[] key, string context) => EncryptString(JsonSerializer.Serialize(value, JsonOptions), key, context);

    public T DecryptObject<T>(string encryptedPayloadJson, byte[] key) => Deserialize<T>(DecryptString(encryptedPayloadJson, key));
    public T DecryptObject<T>(string encryptedPayloadJson, byte[] key, string context) => Deserialize<T>(DecryptString(encryptedPayloadJson, key, context));

    public string EncryptKey(byte[] keyToWrap, byte[] wrappingKey, string context)
    {
        ArgumentNullException.ThrowIfNull(keyToWrap);
        return EncryptBytes(keyToWrap, wrappingKey, ValidateContext(context));
    }

    public byte[] DecryptKey(string encryptedPayloadJson, byte[] wrappingKey, string context) =>
        DecryptBytes(encryptedPayloadJson, wrappingKey, ValidateContext(context), 2);

    private static string EncryptStringCore(string plaintext, byte[] key, byte[]? context)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        var bytes = Encoding.UTF8.GetBytes(plaintext);
        try { return EncryptBytes(bytes, key, context); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private static string DecryptStringCore(string encryptedPayloadJson, byte[] key, byte[]? context, int expectedVersion)
    {
        var plaintext = DecryptBytes(encryptedPayloadJson, key, context, expectedVersion);
        try { return Encoding.UTF8.GetString(plaintext); }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }

    private static string EncryptBytes(byte[] plaintext, byte[] key, byte[]? context)
    {
        ValidateKey(key);
        var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSizeBytes];
        using var aes = new AesGcm(key, TagSizeBytes);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, context);
        return JsonSerializer.Serialize(new EncryptedPayload
        {
            Version = context is null ? 1 : 2,
            Algorithm = "AES-256-GCM",
            NonceBase64 = Convert.ToBase64String(nonce),
            TagBase64 = Convert.ToBase64String(tag),
            CipherTextBase64 = Convert.ToBase64String(ciphertext)
        }, JsonOptions);
    }

    private static byte[] DecryptBytes(string encryptedPayloadJson, byte[] key, byte[]? context, int expectedVersion)
    {
        ValidateKey(key);
        var payload = JsonSerializer.Deserialize<EncryptedPayload>(encryptedPayloadJson, JsonOptions)
            ?? throw new CryptographicException("The encrypted payload is empty or invalid.");
        if (payload.Version != expectedVersion || !string.Equals(payload.Algorithm, "AES-256-GCM", StringComparison.Ordinal))
            throw new CryptographicException("The encrypted payload format is not supported.");

        var nonce = Convert.FromBase64String(payload.NonceBase64);
        var tag = Convert.FromBase64String(payload.TagBase64);
        var ciphertext = Convert.FromBase64String(payload.CipherTextBase64);
        if (nonce.Length != NonceSizeBytes || tag.Length != TagSizeBytes)
            throw new CryptographicException("The encrypted payload is malformed.");

        var plaintext = new byte[ciphertext.Length];
        try
        {
            using var aes = new AesGcm(key, TagSizeBytes);
            aes.Decrypt(nonce, ciphertext, tag, plaintext, context);
            return plaintext;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(plaintext);
            throw;
        }
    }

    private static byte[] ValidateContext(string context)
    {
        if (string.IsNullOrWhiteSpace(context)) throw new ArgumentException("An encryption context is required.", nameof(context));
        return Encoding.UTF8.GetBytes(context);
    }

    private static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, JsonOptions)
        ?? throw new InvalidOperationException("The encrypted payload did not contain valid JSON.");

    private static void ValidateKey(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length != 32) throw new ArgumentException("AES-256-GCM requires a 32-byte key.", nameof(key));
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