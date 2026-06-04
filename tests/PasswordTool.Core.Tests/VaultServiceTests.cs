using OtpNet;
using PasswordTool.Core.Models;
using PasswordTool.Core.Services;

namespace PasswordTool.Core.Tests;

public sealed class VaultServiceTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "PasswordTool.Core.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Vault_storage_is_encrypted_and_requires_master_password_and_totp()
    {
        var storage = new VaultStorageService(tempDirectory);
        var encryption = new EncryptionService();
        var totpService = new TotpService();
        var vaultService = new VaultService(storage, encryption, totpService);
        var secret = totpService.GenerateSecret();
        var code = ComputeTotp(secret);

        vaultService.InitializeNewVault("correct horse battery staple", secret, code);
        var addedItem = vaultService.AddItem(new VaultItem
        {
            Title = "Email",
            Username = "person@example.com",
            Password = "super-secret-value",
            Url = "https://example.com",
            Notes = "private note"
        });

        var encryptedVaultFile = File.ReadAllText(storage.VaultPath);
        var configFile = File.ReadAllText(storage.ConfigPath);

        Assert.DoesNotContain("super-secret-value", encryptedVaultFile);
        Assert.DoesNotContain("person@example.com", encryptedVaultFile);
        Assert.DoesNotContain(secret, configFile);

        vaultService.ClearSession();

        using var reopenedVault = new VaultService(storage, encryption, totpService);
        Assert.False(reopenedVault.TryUnlockMasterPassword("wrong master password", out _));
        Assert.True(reopenedVault.TryUnlockMasterPassword("correct horse battery staple", out _));
        Assert.Throws<InvalidOperationException>(() => reopenedVault.GetItems());

        Assert.True(reopenedVault.VerifyTotpForSession(code));
        Assert.Single(reopenedVault.GetItems());
        Assert.Throws<UnauthorizedAccessException>(() => reopenedVault.GetPassword(addedItem.Id, "123"));
        Assert.Equal("super-secret-value", reopenedVault.GetPassword(addedItem.Id, code));
    }

    public void Dispose()
    {
        if (!Directory.Exists(tempDirectory))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFileSystemEntries(tempDirectory, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }

        Directory.Delete(tempDirectory, recursive: true);
    }

    private static string ComputeTotp(string secretBase32)
    {
        var secretBytes = Base32Encoding.ToBytes(secretBase32);
        return new Totp(secretBytes).ComputeTotp();
    }
}
