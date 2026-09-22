using PasswordTool.Core.Models;
using PasswordTool.Presentation;

namespace PasswordTool.Presentation.Tests;

public sealed class VaultItemEditorInputTests
{
    [Fact]
    public void ToVaultItem_Password_KeepsPasswordFieldsAndNormalizesTags()
    {
        var input = CreateInput(VaultItemType.Password) with
        {
            Password = "secret",
            TotpSecretBase32 = "JBSWY3DPEHPK3PXP",
            RecoveryCodesText = "ignored-one\nignored-two",
            TagsText = "Work, work, Admin"
        };

        var item = input.ToVaultItem();

        Assert.Equal("secret", item.Password);
        Assert.Equal("JBSWY3DPEHPK3PXP", item.TotpSecretBase32);
        Assert.Empty(item.RecoveryCodes);
        Assert.Equal(["Work", "Admin"], item.Tags);
    }

    [Fact]
    public void ToVaultItem_RecoveryCodes_ClearsPasswordFields()
    {
        var input = CreateInput(VaultItemType.RecoveryCodes) with
        {
            Password = "must-not-survive",
            TotpSecretBase32 = "must-not-survive",
            RecoveryCodesText = "alpha-1234\nbeta-5678"
        };

        var item = input.ToVaultItem();

        Assert.Empty(item.Password);
        Assert.Empty(item.TotpSecretBase32);
        Assert.Equal(["alpha-1234", "beta-5678"], item.RecoveryCodes);
    }

    private static VaultItemEditorInput CreateInput(VaultItemType type) => new(
        null,
        type,
        "Example",
        "user",
        string.Empty,
        string.Empty,
        string.Empty,
        "https://example.com",
        string.Empty,
        string.Empty,
        string.Empty,
        false,
        false,
        false);
}
