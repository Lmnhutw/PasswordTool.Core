using PasswordTool.Core.Models;

namespace PasswordTool.Presentation;

public sealed record SettingsSnapshot(
    int InactivityTimeoutMinutes,
    bool NeedsKdfUpgrade,
    DateTimeOffset? LastExternalBackupAt,
    DateTimeOffset? LastVerifiedBackupAt);

public sealed record OperationResult(bool Success, string Message);
