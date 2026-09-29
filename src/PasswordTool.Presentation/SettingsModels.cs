using PasswordTool.Core.Models;

namespace PasswordTool.Presentation;

public sealed record SettingsSnapshot(
    VaultLoginMode LoginMode,
    int InactivityTimeoutMinutes,
    int SensitiveActionTimeoutMinutes,
    int VaultOpenDurationMinutes,
    bool NeedsKdfUpgrade,
    DateTimeOffset? LastExternalBackupAt,
    DateTimeOffset? LastVerifiedBackupAt);

public sealed record LoginModeOption(string Label, VaultLoginMode Value);
public sealed record VaultOpenDurationOption(string Label, int Minutes);
public sealed record OperationResult(bool Success, string Message);
