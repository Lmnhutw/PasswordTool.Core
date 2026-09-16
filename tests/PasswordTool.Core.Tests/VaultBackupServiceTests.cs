using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using PasswordTool.Core.Models;
using PasswordTool.Core.Services;

namespace PasswordTool.Core.Tests;

public sealed class VaultBackupServiceTests
{
    private const string BackupPassphrase = "correct backup horse battery staple";

    [Fact]
    public void Backup_is_encrypted_and_round_trips_typed_items()
    {
        var service = new VaultBackupService();
        var items = CreateItems();

        var json = service.CreateBackup(items, BackupPassphrase, DateTimeOffset.UtcNow);
        var restored = service.ReadBackup(json, BackupPassphrase);

        Assert.DoesNotContain("account-password", json);
        Assert.DoesNotContain("JBSWY3DPEHPK3PXP", json);
        Assert.DoesNotContain("abcd-1234", json);
        Assert.Equal(2, restored.Count);
        Assert.Equal("account-password", restored[0].Password);
        Assert.Equal("JBSWY3DPEHPK3PXP", restored[0].TotpSecretBase32);
        Assert.True(restored[0].IsFavorite);
        Assert.Equal("Personal", restored[0].Folder);
        Assert.Equal(["email", "important"], restored[0].Tags);
        Assert.Equal(VaultItemType.RecoveryCodes, restored[1].Type);
        Assert.Equal(["abcd-1234", "efgh-5678"], restored[1].RecoveryCodes);
    }

    [Fact]
    public void Backup_rejects_wrong_passphrase_and_modified_ciphertext()
    {
        var service = new VaultBackupService();
        var json = service.CreateBackup(CreateItems(), BackupPassphrase, DateTimeOffset.UtcNow);

        Assert.Throws<CryptographicException>(() => service.ReadBackup(json, "incorrect backup passphrase"));

        var document = JsonNode.Parse(json)!.AsObject();
        var encryptedPayload = document["EncryptedPayload"]!.AsObject();
        var ciphertext = encryptedPayload["CipherTextBase64"]!.GetValue<string>();
        encryptedPayload["CipherTextBase64"] = (ciphertext[0] == 'A' ? "B" : "A") + ciphertext[1..];
        var tamperedJson = document.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

        Assert.Throws<CryptographicException>(() => service.ReadBackup(tamperedJson, BackupPassphrase));
    }

    [Fact]
    public void Import_plan_separates_new_duplicate_and_conflicting_ids()
    {
        var service = new VaultBackupService();
        var existing = CreateItems();
        var duplicate = Clone(existing[0]);
        var conflict = Clone(existing[1]);
        conflict.Title = "Changed title";
        var newItem = new VaultItem { Title = "New account", Password = "new-password" };

        var plan = service.CreateImportPlan([duplicate, conflict, newItem], existing);

        Assert.Equal(1, plan.NewItemCount);
        Assert.Equal(1, plan.DuplicateCount);
        Assert.Equal(1, plan.ConflictCount);
    }

    [Fact]
    public void Backup_rejects_items_that_mix_password_and_recovery_code_fields()
    {
        var service = new VaultBackupService();
        var invalid = new VaultItem
        {
            Type = VaultItemType.RecoveryCodes,
            Title = "Mixed item",
            Password = "must-not-be-here",
            RecoveryCodes = ["abcd-1234", "efgh-5678"]
        };

        Assert.Throws<InvalidDataException>(() => service.CreateBackup([invalid], BackupPassphrase, DateTimeOffset.UtcNow));
    }

    private static List<VaultItem> CreateItems()
    {
        return
        [
            new VaultItem
            {
                Title = "Account",
                Username = "person@example.com",
                Password = "account-password",
                TotpSecretBase32 = "JBSWY3DPEHPK3PXP",
                IsFavorite = true,
                Folder = "Personal",
                Tags = ["email", "important"]
            },
            new VaultItem
            {
                Type = VaultItemType.RecoveryCodes,
                Title = "Account recovery",
                Password = string.Empty,
                RecoveryCodes = ["abcd-1234", "efgh-5678"]
            }
        ];
    }

    private static VaultItem Clone(VaultItem item)
    {
        return new VaultItem
        {
            Id = item.Id,
            Type = item.Type,
            Title = item.Title,
            Username = item.Username,
            Password = item.Password,
            TotpSecretBase32 = item.TotpSecretBase32,
            RecoveryCodes = [.. item.RecoveryCodes],
            Url = item.Url,
            HideUrl = item.HideUrl,
            Notes = item.Notes,
            HideNotes = item.HideNotes,
            IsFavorite = item.IsFavorite,
            Folder = item.Folder,
            Tags = [.. item.Tags],
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        };
    }
}
