using OtpNet;
using PasswordTool.Core.Models;
using PasswordTool.Core.Services;
using System.Security.Cryptography;

namespace PasswordTool.Core.Tests;

public sealed class VaultServiceTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "PasswordTool.Core.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Vault_storage_is_encrypted_and_supports_master_password_and_trusted_totp_login()
    {
        var storage = new VaultStorageService(tempDirectory);
        var encryption = new EncryptionService();
        var totpService = new TotpService();
        var vaultService = new VaultService(storage, encryption, totpService);
        var secret = totpService.GenerateSecret();
        var code = ComputeTotp(secret);
        var invalidCode = GetInvalidTotpCode(secret, totpService);

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
        Assert.True(reopenedVault.CanUnlockWithGoogleAuthenticatorToken);
        Assert.False(reopenedVault.TryUnlockWithGoogleAuthenticator(invalidCode, out _));
        Assert.Throws<InvalidOperationException>(() => reopenedVault.GetItems());
        Assert.True(reopenedVault.TryUnlockWithGoogleAuthenticator(code, out _));
        Assert.Single(reopenedVault.GetItems());
        Assert.Throws<UnauthorizedAccessException>(() => reopenedVault.GetPassword(addedItem.Id, invalidCode));
        Assert.Equal("super-secret-value", reopenedVault.GetPassword(addedItem.Id, code));

        reopenedVault.ClearSession();
        Assert.False(reopenedVault.TryUnlockMasterPassword("wrong master password", out _));
        Assert.Throws<InvalidOperationException>(() => reopenedVault.VerifyTotpForSession(code));
        Assert.True(reopenedVault.TryUnlockMasterPassword("correct horse battery staple", out _));
        Assert.True(reopenedVault.IsGoogleAuthenticatorConfigured);
        Assert.Single(reopenedVault.GetItems());
        Assert.Throws<UnauthorizedAccessException>(() => reopenedVault.GetPassword(addedItem.Id, invalidCode));
        Assert.Equal("super-secret-value", reopenedVault.GetPassword(addedItem.Id, code));
    }

    [Fact]
    public void Google_authenticator_login_token_expires_after_one_day()
    {
        var now = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var storage = new VaultStorageService(tempDirectory);
        var encryption = new EncryptionService();
        var totpService = new TotpService();
        var secret = totpService.GenerateSecret();
        var code = ComputeTotp(secret);

        using (var vaultService = new VaultService(storage, encryption, totpService, utcNow: () => now))
        {
            vaultService.InitializeNewVault("correct horse battery staple", secret, code);
            vaultService.AddItem(new VaultItem
            {
                Title = "Email",
                Password = "super-secret-value"
            });
        }

        using (var withinTokenLifetime = new VaultService(storage, encryption, totpService, utcNow: () => now.AddHours(23)))
        {
            Assert.True(withinTokenLifetime.CanUnlockWithGoogleAuthenticatorToken);
            Assert.True(withinTokenLifetime.TryUnlockWithGoogleAuthenticator(code, out _));
            Assert.Single(withinTokenLifetime.GetItems());
        }

        using var afterTokenExpiration = new VaultService(storage, encryption, totpService, utcNow: () => now.AddDays(1).AddSeconds(1));
        Assert.False(afterTokenExpiration.CanUnlockWithGoogleAuthenticatorToken);
        Assert.False(afterTokenExpiration.TryUnlockWithGoogleAuthenticator(code, out var errorMessage));
        Assert.Contains("expired", errorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Throws<InvalidOperationException>(() => afterTokenExpiration.GetItems());
    }

    [Fact]
    public void Legacy_vault_without_totp_secret_unlocks_with_master_password_only()
    {
        var storage = new VaultStorageService(tempDirectory);
        var encryption = new EncryptionService();
        var totpService = new TotpService();
        var masterPasswordService = new MasterPasswordService(encryption);
        const string masterPassword = "correct horse battery staple";
        var config = masterPasswordService.CreateConfig(masterPassword, totpService.GenerateSecret());
        config.EncryptedTotpSecret = string.Empty;
        var key = masterPasswordService.DeriveKey(masterPassword, config);

        try
        {
            storage.SaveConfig(config);
            storage.SaveVaultPayload(encryption.EncryptObject(new VaultData
            {
                Items =
                [
                    new VaultItem
                    {
                        Id = Guid.NewGuid(),
                        Title = "Legacy",
                        Password = "legacy-secret"
                    }
                ]
            }, key));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }

        using var vaultService = new VaultService(storage, encryption, totpService);

        Assert.False(vaultService.TryUnlockMasterPassword("wrong master password", out _));
        Assert.True(vaultService.TryUnlockMasterPassword(masterPassword, out _));
        Assert.False(vaultService.IsGoogleAuthenticatorConfigured);
        Assert.False(vaultService.CanUnlockWithGoogleAuthenticatorToken);

        var item = Assert.Single(vaultService.GetItems());
        Assert.Equal("legacy-secret", vaultService.GetPassword(item.Id, string.Empty));
    }

    [Fact]
    public void Login_mode_defaults_to_hybrid_and_requires_master_password_to_change()
    {
        var storage = new VaultStorageService(tempDirectory);
        var encryption = new EncryptionService();
        var totpService = new TotpService();
        var secret = totpService.GenerateSecret();
        var code = ComputeTotp(secret);
        using var vaultService = new VaultService(storage, encryption, totpService);

        vaultService.InitializeNewVault("correct horse battery staple", secret, code);

        Assert.Equal(VaultLoginMode.Hybrid, vaultService.LoginMode);
        Assert.False(vaultService.TrySetLoginMode("incorrect password", VaultLoginMode.GoogleAuthenticatorCode, out _));
        Assert.Equal(VaultLoginMode.Hybrid, vaultService.LoginMode);
        Assert.True(vaultService.TrySetLoginMode("correct horse battery staple", VaultLoginMode.GoogleAuthenticatorCode, out _));
        Assert.Equal(VaultLoginMode.GoogleAuthenticatorCode, vaultService.LoginMode);
    }

    [Fact]
    public void Hidden_url_and_notes_are_preserved_when_vault_items_are_saved()
    {
        var storage = new VaultStorageService(tempDirectory);
        var encryption = new EncryptionService();
        var totpService = new TotpService();
        var secret = totpService.GenerateSecret();
        using var vaultService = new VaultService(storage, encryption, totpService);

        vaultService.InitializeNewVault("correct horse battery staple", secret, ComputeTotp(secret));
        var item = vaultService.AddItem(new VaultItem
        {
            Title = "Private account",
            Password = "super-secret-value",
            Url = "https://example.com",
            HideUrl = true,
            Notes = "Private note",
            HideNotes = true
        });

        var savedItem = vaultService.GetItemForEditing(item.Id, ComputeTotp(secret));
        Assert.True(savedItem.HideUrl);
        Assert.True(savedItem.HideNotes);
        Assert.Equal("https://example.com", savedItem.Url);
        Assert.Equal("Private note", savedItem.Notes);

        var listItem = Assert.Single(vaultService.GetItems());
        Assert.True(listItem.HideUrl);
        Assert.True(listItem.HideNotes);
        Assert.Empty(listItem.Url);
        Assert.Empty(listItem.Notes);
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

    private static string GetInvalidTotpCode(string secretBase32, TotpService totpService)
    {
        for (var value = 0; value <= 999_999; value++)
        {
            var code = value.ToString("D6");
            if (!totpService.VerifyCode(secretBase32, code))
            {
                return code;
            }
        }

        throw new InvalidOperationException("Could not find an invalid TOTP code.");
    }
}
