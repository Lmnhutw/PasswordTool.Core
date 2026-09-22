using PasswordTool.Core.Models;

namespace PasswordTool.Presentation;

public sealed record TrashItemListItem(Guid Id, string Title, VaultItemType Type, DateTimeOffset? DeletedAt)
{
    public static TrashItemListItem FromVaultItem(VaultItem item) => new(item.Id, item.Title, item.Type, item.DeletedAt);
}
