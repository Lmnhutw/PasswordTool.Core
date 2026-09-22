using PasswordTool.Core.Models;

namespace PasswordTool.Presentation;

public sealed record SettingsSnapshot(
    VaultLoginMode LoginMode,
    int InactivityTimeoutMinutes,
    int SensitiveActionTimeoutMinutes,
    bool NeedsKdfUpgrade,
    DateTimeOffset? LastExternalBackupAt,
    DateTimeOffset? LastVerifiedBackupAt);

public sealed record LoginModeOption(string Label, VaultLoginMode Value);
public sealed record OperationResult(bool Success, string Message);
