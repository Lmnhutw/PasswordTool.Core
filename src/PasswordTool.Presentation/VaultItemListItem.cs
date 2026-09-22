using PasswordTool.Core.Models;

namespace PasswordTool.Presentation;

/// <summary>Secret-free representation used by vault list and search UI.</summary>
public sealed record VaultItemListItem(
    Guid Id,
    string Title,
    string Username,
    VaultItemType Type,
    string Folder,
    IReadOnlyList<string> Tags,
    string Url,
    bool HasTotp,
    bool IsFavorite,
    DateTimeOffset UpdatedAt)
{
    public string TotpDisplay => HasTotp ? "Configured" : "—";
    public string UpdatedDisplay => UpdatedAt.ToLocalTime().ToString("g");

    public static VaultItemListItem FromVaultItem(VaultItem item) => new(
        item.Id,
        item.Title,
        item.Username,
        item.Type,
        item.Folder,
        [.. item.Tags],
        item.HideUrl ? string.Empty : item.Url,
        item.HasTotp,
        item.IsFavorite,
        item.UpdatedAt);
}
