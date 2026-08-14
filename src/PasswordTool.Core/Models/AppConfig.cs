namespace PasswordTool.Core.Models;

public sealed class AppConfig
{
    public int Version { get; set; } = 1;

    public string KdfAlgorithm { get; set; } = "PBKDF2-HMACSHA256";

    public int KdfIterations { get; set; } = 600_000;

    public int KeySizeBytes { get; set; } = 32;

    public string SaltBase64 { get; set; } = string.Empty;

    public string EncryptedTotpSecret { get; set; } = string.Empty;

    public VaultLoginMode LoginMode { get; set; } = VaultLoginMode.Hybrid;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
