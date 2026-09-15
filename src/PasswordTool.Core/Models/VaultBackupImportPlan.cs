namespace PasswordTool.Core.Models;

public sealed record VaultBackupImportPlan(IReadOnlyList<VaultBackupImportPlanItem> Items)
{
    public int NewItemCount => Items.Count(item => item.Status == VaultBackupImportStatus.New);

    public int DuplicateCount => Items.Count(item => item.Status == VaultBackupImportStatus.Duplicate);

    public int ConflictCount => Items.Count(item => item.Status == VaultBackupImportStatus.Conflict);
}

public sealed record VaultBackupImportPlanItem(
    Guid Id,
    string Title,
    VaultItemType Type,
    VaultBackupImportStatus Status);
