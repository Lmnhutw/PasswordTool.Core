using System.Security.Cryptography;
using OtpNet;
using PasswordTool.Core.Models;
using PasswordTool.Core.Services;

namespace PasswordTool.Core.Tests;

public sealed class VaultPasswordLifecycleTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(Path.GetTempPath(), "PasswordTool.Lifecycle.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Password_lifecycle_tracks_only_password_changes_and_type_conversions()
    {
        var now = new DateTimeOffset(2026, 9, 18, 8, 0, 0, TimeSpan.Zero);
        var storage = new VaultStorageService(tempDirectory);
        var totp = new TotpService();
        var secret = totp.GenerateSecret();
        var code = ComputeTotp(secret);
        using var vault = new VaultService(storage, new EncryptionService(), totp, utcNow: () => now);
        vault.InitializeNewVault("correct horse battery staple", secret, code);

        var added = vault.AddItem(new VaultItem { Title = "Email", Password = "first unique password" });
        Assert.Equal(now, added.CreatedAt);
        Assert.Equal(now, added.UpdatedAt);
        Assert.Equal(now, added.PasswordChangedAt);

        now = now.AddMinutes(1);
        var edited = vault.GetItemForEditing(added.Id, code);
        edited.Notes = "Non-secret metadata changed.";
        vault.UpdateItem(edited);
        var afterMetadataEdit = Assert.Single(vault.GetItems());
        Assert.Equal(now, afterMetadataEdit.UpdatedAt);
        Assert.Equal(added.PasswordChangedAt, afterMetadataEdit.PasswordChangedAt);

        now = now.AddMinutes(1);
        edited = vault.GetItemForEditing(added.Id, string.Empty);
        edited.Password = "second unique password";
        vault.UpdateItem(edited);
        var afterPasswordEdit = Assert.Single(vault.GetItems());
        Assert.Equal(now, afterPasswordEdit.PasswordChangedAt);
        var history = Assert.Single(vault.GetPasswordHistory(added.Id, string.Empty));
        Assert.Equal(now, history.ChangedAt);
        Assert.Equal("first unique password", history.Password);

        now = now.AddMinutes(1);
        edited = vault.GetItemForEditing(added.Id, string.Empty);
        edited.Type = VaultItemType.RecoveryCodes;
        edited.Password = string.Empty;
        edited.RecoveryCodes = ["code-one", "code-two"];
        vault.UpdateItem(edited);
        var recoveryCodeItem = Assert.Single(vault.GetItems());
        Assert.Null(recoveryCodeItem.PasswordChangedAt);
        Assert.Empty(vault.GetPasswordHistory(added.Id, string.Empty));

        now = now.AddMinutes(1);
        edited = vault.GetItemForEditing(added.Id, string.Empty);
        edited.Type = VaultItemType.Password;
        edited.Password = "third unique password";
        edited.RecoveryCodes = [];
        vault.UpdateItem(edited);
        Assert.Equal(now, Assert.Single(vault.GetItems()).PasswordChangedAt);
    }

    [Fact]
    public void Legacy_password_dates_use_history_then_updated_at_and_future_dates_cannot_hide_findings()
    {
        var now = new DateTimeOffset(2026, 9, 18, 8, 0, 0, TimeSpan.Zero);
        var storage = new VaultStorageService(tempDirectory);
        var encryption = new EncryptionService();
        var master = new MasterPasswordService(encryption);
        const string masterPassword = "correct horse battery staple";
        var config = master.CreateConfig(masterPassword, string.Empty);
        var key = master.DeriveKey(masterPassword, config);
        var historyDate = now.AddDays(-400);
        var updatedDate = now.AddDays(-500);
        var historyItemId = Guid.NewGuid();
        var futureItemId = Guid.NewGuid();
        try
        {
            storage.SaveState(config, encryption.EncryptObject(new VaultData
            {
                Items =
                [
                    new VaultItem
                    {
                        Id = historyItemId,
                        Title = "History fallback",
                        Password = "history password",
                        CreatedAt = now.AddDays(-600),
                        UpdatedAt = updatedDate,
                        PasswordHistory = [new PasswordHistoryEntry { Password = "older password", ChangedAt = historyDate }]
                    },
                    new VaultItem
                    {
                        Id = futureItemId,
                        Title = "Future lifecycle date",
                        Password = "future password",
                        CreatedAt = now.AddDays(-600),
                        UpdatedAt = updatedDate,
                        PasswordChangedAt = now.AddDays(10)
                    }
                ]
            }, key));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }

        using var vault = new VaultService(storage, encryption, new TotpService(), utcNow: () => now);
        Assert.True(vault.TryUnlockMasterPassword(masterPassword, out var error), error);
        var normalizedHistoryItem = vault.GetItems().Single(item => item.Id == historyItemId);
        Assert.Equal(historyDate, normalizedHistoryItem.PasswordChangedAt);
        var findings = vault.GetSecurityFindings(string.Empty);
        Assert.Contains(findings, finding => finding.ItemId == historyItemId && finding.Type == VaultSecurityFindingType.OldPassword);
        Assert.Contains(findings, finding => finding.ItemId == futureItemId && finding.Type == VaultSecurityFindingType.OldPassword);

        var edited = vault.GetItemForEditing(historyItemId, string.Empty);
        edited.Notes = "A note edit must not reset password age.";
        vault.UpdateItem(edited);
        Assert.Contains(vault.GetSecurityFindings(string.Empty), finding => finding.ItemId == historyItemId && finding.Type == VaultSecurityFindingType.OldPassword);
    }

    [Fact]
    public void Password_lifecycle_survives_reopen_backup_round_trip_and_csv_import()
    {
        var now = new DateTimeOffset(2026, 9, 18, 8, 0, 0, TimeSpan.Zero);
        var storage = new VaultStorageService(tempDirectory);
        var totp = new TotpService();
        var secret = totp.GenerateSecret();
        var code = ComputeTotp(secret);
        using (var vault = new VaultService(storage, new EncryptionService(), totp, utcNow: () => now))
        {
            vault.InitializeNewVault("correct horse battery staple", secret, code);
            var added = vault.AddItem(new VaultItem { Title = "Backup", Password = "backup password" });
            var backupItems = new VaultBackupService().ReadBackup(vault.ExportBackupJson("backup passphrase", code), "backup passphrase");
            Assert.Equal(added.PasswordChangedAt, Assert.Single(backupItems).PasswordChangedAt);
            Assert.Equal(1, vault.ImportCsv("name,username,password\nCSV,user,csv password", string.Empty));
            Assert.Equal(now, vault.GetItems().Single(item => item.Title == "CSV").PasswordChangedAt);
        }

        using var reopened = new VaultService(storage, new EncryptionService(), totp, utcNow: () => now);
        Assert.True(reopened.TryUnlockMasterPassword("correct horse battery staple", out var error), error);
        Assert.All(reopened.GetItems(), item => Assert.Equal(now, item.PasswordChangedAt));
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