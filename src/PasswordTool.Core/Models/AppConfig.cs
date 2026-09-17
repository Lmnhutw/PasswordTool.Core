namespace PasswordTool.Core.Models;

public sealed class AppConfig
{
    public int Version { get; set; } = 2;

    public string KdfAlgorithm { get; set; } = "ARGON2ID";

    public int KdfIterations { get; set; } = 3;

    public int KdfMemorySizeKb { get; set; } = 65_536;

    public int KdfParallelism { get; set; } = 2;

    public int KeySizeBytes { get; set; } = 32;

    public string SaltBase64 { get; set; } = string.Empty;

    public string EncryptedTotpSecret { get; set; } = string.Empty;

    public VaultLoginMode LoginMode { get; set; } = VaultLoginMode.Hybrid;

    public int InactivityLockTimeoutMinutes { get; set; } = VaultSecuritySettings.DefaultInactivityLockTimeoutMinutes;

    public int SensitiveActionTimeoutMinutes { get; set; } = VaultSecuritySettings.DefaultSensitiveActionTimeoutMinutes;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastExternalBackupAt { get; set; }

    public DateTimeOffset? LastVerifiedBackupAt { get; set; }
}
