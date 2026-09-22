namespace PasswordTool.Presentation;

public enum VaultViewFilter
{
    All,
    Favorites,
    Passwords,
    RecoveryCodes
}

public enum VaultSortOrder
{
    TitleAscending,
    TitleDescending,
    UpdatedNewest,
    UpdatedOldest
}

public sealed record VaultFilterOption(string Label, VaultViewFilter Value);
public sealed record VaultSortOption(string Label, VaultSortOrder Value);
public sealed record VaultFolderOption(string Label, string? Folder, bool IsNoFolder = false);
