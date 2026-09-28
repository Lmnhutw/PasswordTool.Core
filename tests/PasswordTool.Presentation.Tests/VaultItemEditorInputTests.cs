using PasswordTool.Core.Models;
using PasswordTool.Presentation;

namespace PasswordTool.Presentation.Tests;

public sealed class VaultItemEditorInputTests
{
    [Fact]
    public void ToVaultItem_Credential_KeepsPasswordAndRecoveryCodesAndNormalizesTags()
    {
        var input = CreateInput() with
        {
            Password = "secret",
            TotpSecretBase32 = "JBSWY3DPEHPK3PXP",
            RecoveryCodesText = "alpha-1234\nbeta-5678",
            TagsText = "Work, work, Admin"
        };

        var item = input.ToVaultItem();

        Assert.Equal("secret", item.Password);
        Assert.Equal("JBSWY3DPEHPK3PXP", item.TotpSecretBase32);
        Assert.Equal(["alpha-1234", "beta-5678"], item.RecoveryCodes);
        Assert.Equal(["Work", "Admin"], item.Tags);
    }

    [Fact]
    public void ToVaultItem_RecoveryCodesCanExistWithoutPassword()
    {
        var input = CreateInput() with
        {
            Password = string.Empty,
            TotpSecretBase32 = string.Empty,
            RecoveryCodesText = "alpha-1234\nbeta-5678"
        };

        var item = input.ToVaultItem();

        Assert.Equal(VaultItemType.Password, item.Type);
        Assert.Empty(item.Password);
        Assert.Empty(item.TotpSecretBase32);
        Assert.Equal(["alpha-1234", "beta-5678"], item.RecoveryCodes);
    }

    private static VaultItemEditorInput CreateInput() => new(
        null,
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
