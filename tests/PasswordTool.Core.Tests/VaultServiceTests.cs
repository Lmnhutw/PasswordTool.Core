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
            var legacyItemId = Guid.NewGuid();
            var legacyJson = $$"""
                {
                  "Version": 1,
                  "Items": [
                    {
                      "Id": "{{legacyItemId}}",
                      "Title": "Legacy",
                      "Username": "",
                      "Password": "legacy-secret",
                      "RecoveryCodes": [],
                      "Url": "",
                      "HideUrl": false,
                      "Notes": "",
                      "HideNotes": false
                    }
                  ]
                }
                """;
            storage.SaveVaultPayload(encryption.EncryptString(legacyJson, key));
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
        Assert.Equal(VaultItemType.Password, item.Type);
        Assert.Equal("legacy-secret", vaultService.GetPassword(item.Id, string.Empty));
    }

    [Fact]
    public void Recovery_codes_and_encrypted_json_backup_round_trip_without_overwriting_existing_items()
    {
        var sourceDirectory = Path.Combine(tempDirectory, "source");
        var destinationDirectory = Path.Combine(tempDirectory, "destination");
        var encryption = new EncryptionService();
        var totpService = new TotpService();
        var sourceSecret = totpService.GenerateSecret();
        var sourceCode = ComputeTotp(sourceSecret);

        string backupJson;
        using (var source = new VaultService(new VaultStorageService(sourceDirectory), encryption, totpService))
        {
            source.InitializeNewVault("source master password", sourceSecret, sourceCode);
            source.AddItem(new VaultItem { Title = "Email", Password = "source-password" });
            source.AddItem(new VaultItem
            {
                Type = VaultItemType.RecoveryCodes,
                Title = "Email recovery",
                RecoveryCodes = ["abcd-1234", "efgh-5678"]
            });
            backupJson = source.ExportBackupJson("correct backup passphrase", sourceCode);
        }

        var destinationStorage = new VaultStorageService(destinationDirectory);
        var destinationSecret = totpService.GenerateSecret();
        var destinationCode = ComputeTotp(destinationSecret);
        using var destination = new VaultService(destinationStorage, encryption, totpService);
        destination.InitializeNewVault("destination master password", destinationSecret, destinationCode);
        var existing = destination.AddItem(new VaultItem { Title = "Existing", Password = "existing-password" });

        var preview = destination.PreviewBackupImport(backupJson, "correct backup passphrase", destinationCode);
        Assert.Equal(2, preview.NewItemCount);
        Assert.Equal(2, destination.ImportBackupJson(backupJson, "correct backup passphrase", destinationCode));
        Assert.Equal(0, destination.ImportBackupJson(backupJson, "correct backup passphrase", destinationCode));

        var items = destination.GetItems();
        Assert.Equal(3, items.Count);
        Assert.Equal("existing-password", destination.GetPassword(existing.Id, destinationCode));
        var recoveryItem = Assert.Single(items, item => item.Type == VaultItemType.RecoveryCodes);
        Assert.Equal(2, recoveryItem.RecoveryCodeCount);
        Assert.Equal(["abcd-1234", "efgh-5678"], destination.GetRecoveryCodes(recoveryItem.Id, destinationCode));

        var encryptedStorage = File.ReadAllText(destinationStorage.VaultPath);
        Assert.DoesNotContain("source-password", encryptedStorage);
        Assert.DoesNotContain("abcd-1234", encryptedStorage);
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
            HideNotes = true,
            IsFavorite = true,
            Folder = "Personal",
            Tags = ["email", "important"],
            TotpSecretBase32 = "JBSWY3DPEHPK3PXP"
        });

        var savedItem = vaultService.GetItemForEditing(item.Id, ComputeTotp(secret));
        Assert.True(savedItem.HideUrl);
        Assert.True(savedItem.HideNotes);
        Assert.Equal("https://example.com", savedItem.Url);
        Assert.Equal("Private note", savedItem.Notes);
        Assert.True(savedItem.IsFavorite);
        Assert.Equal("Personal", savedItem.Folder);
        Assert.Equal(["email", "important"], savedItem.Tags);
        Assert.Equal("JBSWY3DPEHPK3PXP", savedItem.TotpSecretBase32);
        Assert.DoesNotContain("JBSWY3DPEHPK3PXP", File.ReadAllText(storage.VaultPath));

        var listItem = Assert.Single(vaultService.GetItems());
        Assert.True(listItem.HideUrl);
        Assert.True(listItem.HideNotes);
        Assert.Empty(listItem.Url);
        Assert.Empty(listItem.Notes);
        Assert.True(listItem.HasTotp);
        Assert.Empty(listItem.TotpSecretBase32);
    }

    [Fact]
    public void Successful_sensitive_totp_verification_opens_a_five_minute_session()
    {
        var now = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var storage = new VaultStorageService(tempDirectory);
        var totpService = new TotpService();
        var secret = totpService.GenerateSecret();
        var code = ComputeTotp(secret);
        using var vault = new VaultService(storage, new EncryptionService(), totpService, utcNow: () => now);
        vault.InitializeNewVault("correct horse battery staple", secret, code);
        var item = vault.AddItem(new VaultItem { Title = "Email", Password = "secret" });

        Assert.False(vault.IsSensitiveSessionActive);
        Assert.True(vault.VerifyTotpForSensitiveAction(code));
        Assert.True(vault.IsSensitiveSessionActive);
        Assert.Equal("secret", vault.GetPassword(item.Id, string.Empty));

        now = now.AddMinutes(5).AddSeconds(1);
        Assert.False(vault.IsSensitiveSessionActive);
        Assert.Throws<UnauthorizedAccessException>(() => vault.GetPassword(item.Id, "000000"));
    }

    [Fact]
    public void Security_timeouts_require_the_master_password_persist_and_control_sensitive_sessions()
    {
        var now = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var storage = new VaultStorageService(tempDirectory);
        var totpService = new TotpService();
        var secret = totpService.GenerateSecret();
        var code = ComputeTotp(secret);
        using var vault = new VaultService(storage, new EncryptionService(), totpService, utcNow: () => now);
        vault.InitializeNewVault("correct horse battery staple", secret, code);

        Assert.Equal(
            new VaultSecuritySettings(10, 5),
            vault.SecuritySettings);
        Assert.False(vault.TryUpdateSettings(
            "incorrect master password",
            VaultLoginMode.Hybrid,
            new VaultSecuritySettings(20, 2),
            out var wrongPasswordError));
        Assert.Contains("incorrect", wrongPasswordError, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(new VaultSecuritySettings(10, 5), vault.SecuritySettings);

        Assert.True(vault.VerifyTotpForSensitiveAction(code));
        Assert.True(vault.IsSensitiveSessionActive);
        Assert.True(vault.TryUpdateSettings(
            "correct horse battery staple",
            VaultLoginMode.Hybrid,
            new VaultSecuritySettings(20, 2),
            out var updateError), updateError);
        Assert.False(vault.IsSensitiveSessionActive);
        Assert.Equal(new VaultSecuritySettings(20, 2), vault.SecuritySettings);

        Assert.True(vault.VerifyTotpForSensitiveAction(code));
        now = now.AddMinutes(2).AddSeconds(1);
        Assert.False(vault.IsSensitiveSessionActive);

        var persisted = storage.LoadConfig();
        Assert.Equal(20, persisted.InactivityLockTimeoutMinutes);
        Assert.Equal(2, persisted.SensitiveActionTimeoutMinutes);
        using (var reopened = new VaultService(storage, new EncryptionService(), totpService))
        {
            Assert.Equal(new VaultSecuritySettings(20, 2), reopened.SecuritySettings);
        }

        Assert.True(vault.TryChangeMasterPassword(
            "correct horse battery staple",
            "a different secure master password",
            out var changePasswordError), changePasswordError);
        Assert.Equal(new VaultSecuritySettings(20, 2), vault.SecuritySettings);
    }

    [Fact]
    public void Security_timeouts_reject_values_outside_supported_ranges()
    {
        var storage = new VaultStorageService(tempDirectory);
        var totpService = new TotpService();
        var secret = totpService.GenerateSecret();
        var code = ComputeTotp(secret);
        using var vault = new VaultService(storage, new EncryptionService(), totpService);
        vault.InitializeNewVault("correct horse battery staple", secret, code);

        Assert.False(vault.TryUpdateSettings(
            "correct horse battery staple",
            VaultLoginMode.Hybrid,
            new VaultSecuritySettings(0, 5),
            out var inactivityError));
        Assert.Contains("inactivity", inactivityError, StringComparison.OrdinalIgnoreCase);
        Assert.False(vault.TryUpdateSettings(
            "correct horse battery staple",
            VaultLoginMode.Hybrid,
            new VaultSecuritySettings(10, 31),
            out var sensitiveError));
        Assert.Contains("sensitive-action", sensitiveError, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(new VaultSecuritySettings(10, 5), vault.SecuritySettings);
    }

    [Fact]
    public void Legacy_config_without_timeout_fields_uses_secure_defaults()
    {
        Directory.CreateDirectory(tempDirectory);
        File.WriteAllText(
            Path.Combine(tempDirectory, ".config"),
            "{\"Version\":1,\"LoginMode\":0}");
        var storage = new VaultStorageService(tempDirectory);
        using var vault = new VaultService(storage, new EncryptionService(), new TotpService());

        Assert.Equal(new VaultSecuritySettings(10, 5), vault.SecuritySettings);
    }

    [Fact]
    public void Persisted_security_timeouts_are_validated_before_use()
    {
        Directory.CreateDirectory(tempDirectory);
        File.WriteAllText(
            Path.Combine(tempDirectory, ".config"),
            "{\"InactivityLockTimeoutMinutes\":0,\"SensitiveActionTimeoutMinutes\":5}");
        var storage = new VaultStorageService(tempDirectory);
        using var vault = new VaultService(storage, new EncryptionService(), new TotpService());

        var error = Assert.Throws<ArgumentOutOfRangeException>(() => vault.SecuritySettings);
        Assert.Contains("inactivity", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Csv_import_previews_adds_and_then_skips_matching_accounts()
    {
        var storage = new VaultStorageService(tempDirectory);
        var totpService = new TotpService();
        var secret = totpService.GenerateSecret();
        var code = ComputeTotp(secret);
        using var vault = new VaultService(storage, new EncryptionService(), totpService);
        vault.InitializeNewVault("correct horse battery staple", secret, code);
        const string csv = "name,url,username,password,login_totp\nEmail,https://example.com,user@example.com,password,JBSWY3DPEHPK3PXP";

        var plan = vault.PreviewCsvImport(csv, code);
        Assert.Equal(1, plan.NewItemCount);
        Assert.Equal(1, vault.ImportCsv(csv, string.Empty));
        Assert.Equal(0, vault.ImportCsv(csv, string.Empty));

        var item = Assert.Single(vault.GetItems());
        Assert.True(item.HasTotp);
        Assert.Equal("password", vault.GetPassword(item.Id, string.Empty));
        Assert.DoesNotContain("password", File.ReadAllText(storage.VaultPath));
        Assert.DoesNotContain("JBSWY3DPEHPK3PXP", File.ReadAllText(storage.VaultPath));
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
