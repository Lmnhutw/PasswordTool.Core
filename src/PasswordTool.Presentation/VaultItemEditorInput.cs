using PasswordTool.Core.Models;
using PasswordTool.Core.Services;

namespace PasswordTool.Presentation;

public sealed record VaultItemEditorInput(
    Guid? Id,
    VaultItemType Type,
    string Title,
    string Username,
    string Password,
    string RecoveryCodesText,
    string TotpSecretBase32,
    string Url,
    string Notes,
    string Folder,
    string TagsText,
    bool IsFavorite,
    bool HideUrl,
    bool HideNotes)
{
    public VaultItem ToVaultItem()
    {
        var recoveryCodes = new List<string>();
        if (Type == VaultItemType.RecoveryCodes)
        {
            var parsed = RecoveryCodeParser.Parse(RecoveryCodesText);
            if (!parsed.IsValid) throw new ArgumentException(parsed.ErrorMessage, nameof(RecoveryCodesText));
            recoveryCodes = [.. parsed.Codes];
        }

        var tags = TagsText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new VaultItem
        {
            Id = Id ?? Guid.NewGuid(),
            Type = Type,
            Title = Title,
            Username = Username,
            Password = Type == VaultItemType.Password ? Password : string.Empty,
            RecoveryCodes = recoveryCodes,
            TotpSecretBase32 = Type == VaultItemType.Password ? TotpSecretBase32 : string.Empty,
            Url = Url,
            Notes = Notes,
            Folder = Folder,
            Tags = tags,
            IsFavorite = IsFavorite,
            HideUrl = HideUrl,
            HideNotes = HideNotes
        };
    }
}
