using PasswordTool.Core.Models;
using PasswordTool.Presentation;

namespace PasswordTool.Presentation.Tests;

public sealed class VaultItemListItemTests
{
    [Fact]
    public void FromVaultItem_ProjectsOnlyListMetadata()
    {
        var source = new VaultItem
        {
            Title = "Example",
            Username = "user",
            Password = "secret",
            TotpSecretBase32 = "totp-secret",
            RecoveryCodes = ["code-one", "code-two"],
            Tags = ["Work"],
            HasTotp = true
        };

        var projected = VaultItemListItem.FromVaultItem(source);

        Assert.Equal("Example", projected.Title);
        Assert.Equal(["Work"], projected.Tags);
        Assert.True(projected.HasTotp);
        var representation = projected.ToString();
        Assert.DoesNotContain("secret", representation, StringComparison.Ordinal);
        Assert.DoesNotContain("code-one", representation, StringComparison.Ordinal);
    }
}
