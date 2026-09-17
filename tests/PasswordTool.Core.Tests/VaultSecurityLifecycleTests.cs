using System.Security.Cryptography;
using System.Text.Json;
using OtpNet;
using PasswordTool.Core.Models;
using PasswordTool.Core.Services;

namespace PasswordTool.Core.Tests;

public sealed class VaultSecurityLifecycleTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(Path.GetTempPath(), "PasswordTool.Security.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void New_configs_use_argon2id_and_legacy_pbkdf2_configs_remain_unlockable()
    {
        var encryption = new EncryptionService();
        var service = new MasterPasswordService(encryption);
        const string password = "correct horse battery staple";
        var current = service.CreateConfig(password, string.Empty);
        Assert.Equal(MasterPasswordService.Argon2idAlgorithm, current.KdfAlgorithm);
        Assert.False(MasterPasswordService.NeedsKdfUpgrade(current));

        var legacy = new AppConfig
        {
            Version = 1,
            KdfAlgorithm = MasterPasswordService.Pbkdf2Algorithm,
            KdfIterations = MasterPasswordService.DefaultPbkdf2Iterations,
            KeySizeBytes = MasterPasswordService.DefaultKeySizeBytes,
            SaltBase64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(MasterPasswordService.SaltSizeBytes))
        };
        var key = service.DeriveKey(password, legacy);
        try
        {
            legacy.EncryptedTotpSecret = encryption.EncryptString("JBSWY3DPEHPK3PXP", key);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }

        Assert.True(service.TryUnlockConfig(password, legacy, out var unlockedKey, out var secret));
        CryptographicOperations.ZeroMemory(unlockedKey);
        Assert.Equal("JBSWY3DPEHPK3PXP", secret);
        Assert.True(MasterPasswordService.NeedsKdfUpgrade(legacy));
    }

    [Fact]
    public void Changing_master_password_reencrypts_the_vault_and_invalidates_the_old_password()
    {
        var storage = new VaultStorageService(tempDirectory);
        var totp = new TotpService();
        var secret = totp.GenerateSecret();
        using var vault = new VaultService(storage, new EncryptionService(), totp);
        vault.InitializeNewVault("correct horse battery staple", secret, ComputeTotp(secret));
        vault.AddItem(new VaultItem { Title = "Email", Password = "account secret" });

        Assert.True(vault.TryChangeMasterPassword("correct horse battery staple", "a completely different master password", out var error), error);
        vault.ClearSession();
        Assert.False(vault.TryUnlockMasterPassword("correct horse battery staple", out _));
        Assert.True(vault.TryUnlockMasterPassword("a completely different master password", out error), error);
        Assert.Single(vault.GetItems());
        Assert.DoesNotContain("account secret", File.ReadAllText(storage.VaultPath));
    }

    [Fact]
    public void Password_history_trash_and_local_security_check_work_without_exposing_secrets()
    {
        var now = new DateTimeOffset(2026, 9, 16, 0, 0, 0, TimeSpan.Zero);
        var storage = new VaultStorageService(tempDirectory);
        var totp = new TotpService();
        var secret = totp.GenerateSecret();
        var code = ComputeTotp(secret);
        using var vault = new VaultService(storage, new EncryptionService(), totp, utcNow: () => now);
        vault.InitializeNewVault("correct horse battery staple", secret, code);
        now = now.AddDays(-400);
        var first = vault.AddItem(new VaultItem { Title = "Old weak account", Password = "duplicate" });
        var second = vault.AddItem(new VaultItem { Title = "Second weak account", Password = "duplicate" });
        vault.AddItem(new VaultItem { Title = "Third weak account", Password = "duplicate" });
        now = now.AddDays(400);

        var editable = vault.GetItemForEditing(first.Id, code);
        for (var index = 0; index < 11; index++)
        {
            editable.Password = $"replacement-{index:D2}-password";
            vault.UpdateItem(editable);
        }
        Assert.Equal(10, vault.GetPasswordHistory(first.Id, string.Empty).Count);

        var findings = vault.GetSecurityFindings(string.Empty);
        Assert.Contains(findings, finding => finding.ItemId == second.Id && finding.Type == VaultSecurityFindingType.WeakPassword);
        Assert.Contains(findings, finding => finding.ItemId == second.Id && finding.Type == VaultSecurityFindingType.ReusedPassword);
        Assert.Contains(findings, finding => finding.ItemId == second.Id && finding.Type == VaultSecurityFindingType.OldPassword);
        Assert.DoesNotContain(findings, finding => finding.Description.Contains("duplicate", StringComparison.Ordinal));

        vault.DeleteItem(second.Id);
        Assert.DoesNotContain(vault.GetItems(), item => item.Id == second.Id);
        Assert.Contains(vault.GetDeletedItems(), item => item.Id == second.Id);
        vault.RestoreDeletedItem(second.Id);
        Assert.Contains(vault.GetItems(), item => item.Id == second.Id);
        Assert.DoesNotContain("duplicate", File.ReadAllText(storage.VaultPath));
    }

    [Fact]
    public void Restoring_a_snapshot_restores_config_and_vault_as_one_pair_then_requires_unlock()
    {
        var storage = new VaultStorageService(tempDirectory);
        var totp = new TotpService();
        var secret = totp.GenerateSecret();
        using var vault = new VaultService(storage, new EncryptionService(), totp);
        const string master = "correct horse battery staple";
        vault.InitializeNewVault(master, secret, ComputeTotp(secret));
        vault.AddItem(new VaultItem { Title = "First", Password = "first secret" });
        vault.AddItem(new VaultItem { Title = "Second", Password = "second secret" });
        var snapshot = vault.GetSnapshots().First();

        Assert.True(vault.TryRestoreSnapshot(snapshot.Id, master, out var error), error);
        Assert.Throws<InvalidOperationException>(() => vault.GetItems());
        Assert.True(vault.TryUnlockMasterPassword(master, out error), error);
        var item = Assert.Single(vault.GetItems());
        Assert.Equal("First", item.Title);
    }

    [Fact]
    public void Resetting_authenticator_replaces_the_secret_and_refreshes_the_trusted_token()
    {
        var storage = new VaultStorageService(tempDirectory);
        var totp = new TotpService();
        var oldSecret = totp.GenerateSecret();
        var oldCode = ComputeTotp(oldSecret);
        var newSecret = totp.GenerateSecret();
        while (ComputeTotp(newSecret) == oldCode) newSecret = totp.GenerateSecret();
        var newCode = ComputeTotp(newSecret);
        using var vault = new VaultService(storage, new EncryptionService(), totp);
        const string master = "correct horse battery staple";
        vault.InitializeNewVault(master, oldSecret, oldCode);

        Assert.True(vault.TryResetAuthenticator(master, newSecret, newCode, out var error), error);
        vault.ClearSession();
        Assert.False(vault.TryUnlockWithGoogleAuthenticator(oldCode, out _));
        Assert.True(vault.TryUnlockWithGoogleAuthenticator(newCode, out error), error);
        Assert.DoesNotContain(newSecret, File.ReadAllText(storage.ConfigPath));
    }

    [Fact]
    public void Storage_keeps_only_the_five_most_recent_complete_snapshots()
    {
        var storage = new VaultStorageService(tempDirectory);
        var encryption = new EncryptionService();
        var master = new MasterPasswordService(encryption);
        var config = master.CreateConfig("correct horse battery staple", string.Empty);
        var key = master.DeriveKey("correct horse battery staple", config);
        try
        {
            storage.SaveConfig(config);
            for (var index = 0; index < 8; index++)
            {
                storage.SaveVaultPayload(encryption.EncryptObject(new VaultData
                {
                    Items = [new VaultItem { Title = $"Item {index}", Password = $"password {index}" }]
                }, key));
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }

        var snapshots = storage.GetSnapshots();
        Assert.Equal(5, snapshots.Count);
        Assert.All(snapshots, snapshot =>
        {
            var directory = Path.Combine(storage.SnapshotsDirectory, snapshot.Id);
            Assert.True(File.Exists(Path.Combine(directory, ".config")));
            Assert.True(File.Exists(Path.Combine(directory, ".storage")));
        });
    }

    [Fact]
    public void Interrupted_paired_state_transaction_is_completed_on_the_next_open()
    {
        var storage = new VaultStorageService(tempDirectory);
        var encryption = new EncryptionService();
        var master = new MasterPasswordService(encryption);
        var config = master.CreateConfig("correct horse battery staple", string.Empty);
        var key = master.DeriveKey("correct horse battery staple", config);
        try
        {
            storage.SaveConfig(config);
            storage.SaveVaultPayload(encryption.EncryptObject(new VaultData(), key));
            var intendedVault = encryption.EncryptObject(new VaultData
            {
                Items = [new VaultItem { Title = "Recovered", Password = "encrypted secret" }]
            }, key);
            File.WriteAllText(Path.Combine(tempDirectory, ".state-transaction"), JsonSerializer.Serialize(new
            {
                ConfigJson = File.ReadAllText(storage.ConfigPath),
                VaultJson = intendedVault
            }));

            var recoveredStorage = new VaultStorageService(tempDirectory);
            var recovered = encryption.DecryptObject<VaultData>(recoveredStorage.LoadVaultPayload(), key);
            Assert.Equal("Recovered", Assert.Single(recovered.Items).Title);
            Assert.False(File.Exists(Path.Combine(tempDirectory, ".state-transaction")));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public void Dispose()
    {
        if (!Directory.Exists(tempDirectory)) return;
        foreach (var path in Directory.EnumerateFileSystemEntries(tempDirectory, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }
        Directory.Delete(tempDirectory, recursive: true);
    }

    private static string ComputeTotp(string secretBase32) => new Totp(Base32Encoding.ToBytes(secretBase32)).ComputeTotp();
}
