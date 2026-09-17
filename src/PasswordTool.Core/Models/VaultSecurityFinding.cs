namespace PasswordTool.Core.Models;

public enum VaultSecurityFindingType
{
    WeakPassword,
    ReusedPassword,
    OldPassword
}

public sealed record VaultSecurityFinding(Guid ItemId, string Title, VaultSecurityFindingType Type, string Description);
