namespace PasswordTool.Core.Models;

public enum VaultSecurityFindingType
{
    WeakPassword,
    ReusedPassword,
    OldPassword
}

public sealed record VaultSecurityFinding(Guid ItemId, string Title, VaultSecurityFindingType Type, string Description)
{
    public string DisplayLabel => Type switch
    {
        VaultSecurityFindingType.WeakPassword => "Weak password",
        VaultSecurityFindingType.ReusedPassword => "Reused password",
        VaultSecurityFindingType.OldPassword => "Password older than one year",
        _ => "Security finding"
    };

    public string Recommendation => Description;

    public DateTimeOffset? PasswordChangedAt { get; init; }
}
