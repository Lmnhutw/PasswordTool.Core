namespace PasswordTool.Core.Models;

public sealed record VaultBackupInspection(
    string Format,
    int Version,
    DateTimeOffset? CreatedAt,
    int TotalItemCount,
    int PasswordItemCount,
    int RecoveryCodeItemCount,
    int ActiveItemCount,
    int TrashItemCount);
