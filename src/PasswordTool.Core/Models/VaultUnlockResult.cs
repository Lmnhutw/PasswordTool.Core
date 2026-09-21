namespace PasswordTool.Core.Models;

public enum VaultUnlockStatus
{
    Failed,
    Unlocked,
    UnlockedAndMigrated,
    UnlockedMigrationDeferred
}

public sealed record VaultUnlockResult(VaultUnlockStatus Status, string Message = "")
{
    public bool Success => Status != VaultUnlockStatus.Failed;
}