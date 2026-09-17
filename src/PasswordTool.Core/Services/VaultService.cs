using System.Security.Cryptography;
using System.Text;
using System.ComponentModel;
using PasswordTool.Core.Models;

namespace PasswordTool.Core.Services;

public sealed class VaultService : IDisposable
{
    private readonly EncryptionService encryptionService;
    private readonly MasterPasswordService masterPasswordService;
    private readonly TotpService totpService;
    private readonly TrustedUnlockTokenService trustedUnlockTokenService;
    private readonly VaultBackupService backupService;
    private readonly VaultCsvImportService csvImportService;
    private readonly PasswordGeneratorService passwordGeneratorService;
    private readonly VaultStorageService storageService;
    private readonly Func<DateTimeOffset> utcNow;

    private byte[]? encryptionKey;
    private string? totpSecretBase32;
    private VaultData? vaultData;
    private bool isVaultOpen;
    private DateTimeOffset? sensitiveSessionExpiresAt;
    private bool disposed;

    public VaultService()
        : this(new VaultStorageService(), new EncryptionService(), new TotpService())
    {
    }

    public VaultService(
        VaultStorageService storageService,
        EncryptionService encryptionService,
        TotpService totpService,
        TrustedUnlockTokenService? trustedUnlockTokenService = null,
        Func<DateTimeOffset>? utcNow = null,
        VaultCsvImportService? csvImportService = null)
    {
        this.storageService = storageService;
        this.encryptionService = encryptionService;
        this.totpService = totpService;
        this.trustedUnlockTokenService = trustedUnlockTokenService ?? new TrustedUnlockTokenService();
        backupService = new VaultBackupService(encryptionService);
        this.csvImportService = csvImportService ?? new VaultCsvImportService(totpService);
        passwordGeneratorService = new PasswordGeneratorService();
        this.utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        masterPasswordService = new MasterPasswordService(encryptionService);
    }

    public bool IsInitialized => storageService.IsInitialized;

    public bool HasPartialStorage => storageService.HasPartialStorage;

    public string StorageDirectory => storageService.AppDirectory;

    public string ConfigPath => storageService.ConfigPath;

    public string VaultPath => storageService.VaultPath;

    public string TrustedUnlockTokenPath => storageService.TrustedUnlockTokenPath;

    public bool IsSensitiveSessionActive => sensitiveSessionExpiresAt is { } expiresAt && expiresAt > utcNow();

    public DateTimeOffset? SensitiveSessionExpiresAt => IsSensitiveSessionActive ? sensitiveSessionExpiresAt : null;

    public bool IsGoogleAuthenticatorConfigured
    {
        get
        {
            ThrowIfDisposed();
            return !string.IsNullOrWhiteSpace(totpSecretBase32);
        }
    }

    public bool CanUnlockWithGoogleAuthenticatorToken
    {
        get
        {
            ThrowIfDisposed();

            try
            {
                var config = storageService.LoadConfig();
                if (string.IsNullOrWhiteSpace(config.EncryptedTotpSecret) || !storageService.HasTrustedUnlockToken)
                {
                    return false;
                }

                var token = storageService.LoadTrustedUnlockToken();
                return trustedUnlockTokenService.IsTokenUsable(token, ComputeConfigFingerprint(config), utcNow());
            }
            catch (Exception ex) when (ex is IOException
                or UnauthorizedAccessException
                or InvalidOperationException
                or CryptographicException
                or FormatException
                or System.Text.Json.JsonException
                or PlatformNotSupportedException)
            {
                return false;
            }
        }
    }

    public VaultLoginMode LoginMode
    {
        get
        {
            ThrowIfDisposed();
            return storageService.LoadConfig().LoginMode;
        }
    }

    public bool NeedsKdfUpgrade
    {
        get
        {
            ThrowIfDisposed();
            return MasterPasswordService.NeedsKdfUpgrade(storageService.LoadConfig());
        }
    }

    public DateTimeOffset? LastExternalBackupAt
    {
        get
        {
            ThrowIfDisposed();
            return storageService.LoadConfig().LastExternalBackupAt;
        }
    }

    public DateTimeOffset? LastVerifiedBackupAt
    {
        get
        {
            ThrowIfDisposed();
            return storageService.LoadConfig().LastVerifiedBackupAt;
        }
    }

    public void InitializeNewVault(string masterPassword, string totpSecretBase32, string confirmationTotpCode)
    {
        ThrowIfDisposed();

        if (storageService.HasConfig || storageService.HasVault)
        {
            throw new InvalidOperationException("PasswordTool storage already exists.");
        }

        if (!totpService.IsSecretValid(totpSecretBase32))
        {
            throw new ArgumentException("The generated TOTP secret is invalid.", nameof(totpSecretBase32));
        }

        if (!totpService.VerifyCode(totpSecretBase32, confirmationTotpCode))
        {
            throw new UnauthorizedAccessException("Authenticator setup could not be verified.");
        }

        var config = masterPasswordService.CreateConfig(masterPassword, totpSecretBase32);
        var key = masterPasswordService.DeriveKey(masterPassword, config);

        ClearSession();

        encryptionKey = key;
        this.totpSecretBase32 = totpSecretBase32;
        vaultData = new VaultData();
        isVaultOpen = true;

        storageService.SaveConfig(config);
        SaveVault();
        SaveTrustedUnlockToken(config);
    }

    public VaultBackupInspection InspectBackupJson(string backupJson, string passphrase)
    {
        ThrowIfDisposed();
        return backupService.InspectBackup(backupJson, passphrase);
    }

    public VaultBackupInspection VerifyBackupJson(string backupJson, string passphrase)
    {
        ThrowIfDisposed();
        EnsureOpen();
        var inspection = backupService.InspectBackup(backupJson, passphrase);
        UpdateBackupHealth(lastVerifiedBackupAt: utcNow());
        return inspection;
    }

    public VaultBackupInspection VerifyExternalBackupFile(string backupPath, string passphrase)
    {
        return VerifyBackupJson(ReadBoundedBackupFile(backupPath), passphrase);
    }

    public void CreateExternalBackupFile(string destinationPath, string passphrase, string totpCode)
    {
        ThrowIfDisposed();
        EnsureOpen();
        RequireSensitiveTotp(totpCode);

        var backupJson = backupService.CreateBackup(vaultData!.Items, passphrase, utcNow());
        WriteExternalBackupFile(destinationPath, backupJson);
        UpdateBackupHealth(lastExternalBackupAt: utcNow());
    }

    public void RecoverFromBackup(VaultRecoveryRequest request)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        if (storageService.HasConfig || storageService.HasVault)
        {
            throw new InvalidOperationException("Recovery is available only when no PasswordTool storage exists.");
        }

        MasterPasswordService.ValidateNewMasterPassword(request.NewMasterPassword);
        if (!totpService.IsSecretValid(request.NewTotpSecretBase32))
        {
            throw new ArgumentException("The generated TOTP secret is invalid.", nameof(request));
        }
        if (!totpService.VerifyCode(request.NewTotpSecretBase32, request.TotpConfirmationCode))
        {
            throw new UnauthorizedAccessException("Authenticator setup could not be verified.");
        }

        var recoveredItems = backupService.ReadBackup(request.BackupJson, request.BackupPassphrase);
        var now = utcNow();
        var config = masterPasswordService.CreateConfig(request.NewMasterPassword, request.NewTotpSecretBase32);
        config.CreatedAt = now;
        config.UpdatedAt = now;
        config.LastVerifiedBackupAt = now;
        var key = masterPasswordService.DeriveKey(request.NewMasterPassword, config);
        try
        {
            var recoveredVault = new VaultData
            {
                Items = recoveredItems.Select(item => Clone(item, includePassword: true)).ToList()
            };
            var encryptedVaultJson = encryptionService.EncryptObject(recoveredVault, key);
            storageService.SaveState(config, encryptedVaultJson);

            ClearSession();
            encryptionKey = key;
            key = [];
            totpSecretBase32 = request.NewTotpSecretBase32;
            vaultData = recoveredVault;
            isVaultOpen = true;
            SaveTrustedUnlockToken(config);
        }
        finally
        {
            if (key.Length > 0)
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }
    }

    public bool TryUnlockMasterPassword(string masterPassword, out string errorMessage)
    {
        ThrowIfDisposed();
        ClearSession();
        errorMessage = string.Empty;

        try
        {
            var config = storageService.LoadConfig();
            if (!masterPasswordService.TryUnlockConfig(
                masterPassword,
                config,
                out var derivedKey,
                out var decryptedTotpSecret))
            {
                errorMessage = "The Master Password is incorrect.";
                return false;
            }

            var encryptedVaultJson = storageService.LoadVaultPayload();
            var decryptedVault = encryptionService.DecryptObject<VaultData>(encryptedVaultJson, derivedKey);
            decryptedVault.Items ??= [];
            NormalizeItems(decryptedVault.Items);

            encryptionKey = derivedKey;
            totpSecretBase32 = string.IsNullOrWhiteSpace(decryptedTotpSecret)
                ? null
                : decryptedTotpSecret;
            vaultData = decryptedVault;
            isVaultOpen = true;
            PurgeExpiredTrash();
            SaveTrustedUnlockToken(config);
            return true;
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or CryptographicException
            or FormatException
            or PlatformNotSupportedException
            or System.Text.Json.JsonException)
        {
            ClearSession();
            errorMessage = "The Master Password is incorrect, or PasswordTool storage could not be opened.";
            return false;
        }
    }

    public bool TryUnlockWithGoogleAuthenticator(string code, out string errorMessage)
    {
        ThrowIfDisposed();
        ClearSession();
        errorMessage = string.Empty;
        byte[]? trustedKey = null;

        if (string.IsNullOrWhiteSpace(code))
        {
            errorMessage = "Google Authenticator code is required.";
            return false;
        }

        try
        {
            var config = storageService.LoadConfig();
            if (string.IsNullOrWhiteSpace(config.EncryptedTotpSecret))
            {
                errorMessage = "Google Authenticator login is not configured for this vault.";
                return false;
            }

            if (!storageService.HasTrustedUnlockToken)
            {
                errorMessage = "Enter the Master Password to create a 1-day Google Authenticator login token.";
                return false;
            }

            var configFingerprint = ComputeConfigFingerprint(config);
            var token = storageService.LoadTrustedUnlockToken();
            if (!trustedUnlockTokenService.TryUnprotectEncryptionKey(
                token,
                configFingerprint,
                utcNow(),
                out trustedKey,
                out errorMessage))
            {
                TryDeleteTrustedUnlockToken();
                return false;
            }

            var decryptedTotpSecret = encryptionService.DecryptString(config.EncryptedTotpSecret, trustedKey);
            if (!totpService.VerifyCode(decryptedTotpSecret, code))
            {
                errorMessage = "Invalid Google Authenticator code.";
                return false;
            }

            var encryptedVaultJson = storageService.LoadVaultPayload();
            var decryptedVault = encryptionService.DecryptObject<VaultData>(encryptedVaultJson, trustedKey);
            decryptedVault.Items ??= [];
            NormalizeItems(decryptedVault.Items);

            encryptionKey = trustedKey;
            trustedKey = null;
            totpSecretBase32 = decryptedTotpSecret;
            vaultData = decryptedVault;
            isVaultOpen = true;
            PurgeExpiredTrash();
            return true;
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or CryptographicException
            or FormatException
            or System.Text.Json.JsonException)
        {
            ClearSession();
            errorMessage = "Google Authenticator login could not open PasswordTool storage. Enter the Master Password to create a new 1-day token.";
            return false;
        }
        finally
        {
            if (trustedKey is { Length: > 0 })
            {
                CryptographicOperations.ZeroMemory(trustedKey);
            }
        }
    }

    public bool VerifyTotpForSession(string code)
    {
        ThrowIfDisposed();
        EnsureMasterPasswordUnlocked();

        if (totpSecretBase32 is null)
        {
            isVaultOpen = true;
            return true;
        }

        if (!totpService.VerifyCode(totpSecretBase32!, code))
        {
            return false;
        }

        isVaultOpen = true;
        return true;
    }

    public bool VerifyTotpForSensitiveAction(string code)
    {
        ThrowIfDisposed();
        EnsureOpen();
        if (totpSecretBase32 is null)
        {
            return true;
        }

        if (IsSensitiveSessionActive)
        {
            return true;
        }

        if (!totpService.VerifyCode(totpSecretBase32, code))
        {
            return false;
        }

        sensitiveSessionExpiresAt = utcNow().AddMinutes(5);
        return true;
    }

    public void ClearSensitiveSession()
    {
        ThrowIfDisposed();
        sensitiveSessionExpiresAt = null;
    }

    public bool TrySetLoginMode(string masterPassword, VaultLoginMode loginMode, out string errorMessage)
    {
        ThrowIfDisposed();
        EnsureOpen();
        errorMessage = string.Empty;

        if (!Enum.IsDefined(loginMode))
        {
            errorMessage = "The selected login mode is not supported.";
            return false;
        }

        byte[]? verificationKey = null;
        try
        {
            var config = storageService.LoadConfig();
            if (!TryVerifyCurrentMasterPassword(masterPassword, config, out verificationKey))
            {
                errorMessage = "The Master Password is incorrect.";
                return false;
            }

            config.LoginMode = loginMode;
            config.UpdatedAt = utcNow();
            storageService.SaveConfig(config);
            return true;
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or CryptographicException
            or FormatException
            or System.Text.Json.JsonException)
        {
            errorMessage = "The login setting could not be saved.";
            return false;
        }
        finally
        {
            if (verificationKey is { Length: > 0 })
            {
                CryptographicOperations.ZeroMemory(verificationKey);
            }
        }
    }

    public bool TryChangeMasterPassword(string currentMasterPassword, string newMasterPassword, out string errorMessage)
    {
        ThrowIfDisposed();
        EnsureOpen();
        errorMessage = string.Empty;
        byte[]? verificationKey = null;
        byte[]? newKey = null;

        try
        {
            var oldConfig = storageService.LoadConfig();
            if (!TryVerifyCurrentMasterPassword(currentMasterPassword, oldConfig, out verificationKey))
            {
                errorMessage = "The current Master Password is incorrect.";
                return false;
            }

            var newConfig = masterPasswordService.CreateConfig(newMasterPassword, totpSecretBase32 ?? string.Empty);
            newConfig.CreatedAt = oldConfig.CreatedAt;
            newConfig.UpdatedAt = utcNow();
            newConfig.LoginMode = oldConfig.LoginMode;
            newKey = masterPasswordService.DeriveKey(newMasterPassword, newConfig);
            var payload = encryptionService.EncryptObject(vaultData, newKey);
            storageService.SaveState(newConfig, payload);

            CryptographicOperations.ZeroMemory(encryptionKey!);
            encryptionKey = newKey;
            newKey = null;
            sensitiveSessionExpiresAt = null;
            TryDeleteTrustedUnlockToken();
            SaveTrustedUnlockToken(newConfig);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException
            or InvalidOperationException or CryptographicException or FormatException or NotSupportedException)
        {
            errorMessage = ex is ArgumentException ? ex.Message : "The Master Password could not be changed.";
            return false;
        }
        finally
        {
            if (verificationKey is { Length: > 0 }) CryptographicOperations.ZeroMemory(verificationKey);
            if (newKey is { Length: > 0 }) CryptographicOperations.ZeroMemory(newKey);
        }
    }

    public bool TryUpgradeKdf(string masterPassword, out string errorMessage)
    {
        ThrowIfDisposed();
        EnsureOpen();
        if (!NeedsKdfUpgrade)
        {
            errorMessage = string.Empty;
            return true;
        }

        return TryChangeMasterPassword(masterPassword, masterPassword, out errorMessage);
    }

    public bool TryResetAuthenticator(
        string masterPassword,
        string newTotpSecretBase32,
        string confirmationCode,
        out string errorMessage)
    {
        ThrowIfDisposed();
        EnsureOpen();
        errorMessage = string.Empty;
        byte[]? verificationKey = null;

        try
        {
            var config = storageService.LoadConfig();
            if (!TryVerifyCurrentMasterPassword(masterPassword, config, out verificationKey))
            {
                errorMessage = "The Master Password is incorrect.";
                return false;
            }

            if (!totpService.IsSecretValid(newTotpSecretBase32)
                || !totpService.VerifyCode(newTotpSecretBase32, confirmationCode))
            {
                errorMessage = "The new Authenticator secret or confirmation code is invalid.";
                return false;
            }

            config.EncryptedTotpSecret = encryptionService.EncryptString(newTotpSecretBase32, encryptionKey!);
            config.UpdatedAt = utcNow();
            storageService.SaveState(config, encryptionService.EncryptObject(vaultData, encryptionKey!));
            totpSecretBase32 = newTotpSecretBase32;
            sensitiveSessionExpiresAt = null;
            TryDeleteTrustedUnlockToken();
            SaveTrustedUnlockToken(config);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException
            or InvalidOperationException or CryptographicException or FormatException or NotSupportedException)
        {
            errorMessage = "The PasswordTool Authenticator could not be reset.";
            return false;
        }
        finally
        {
            if (verificationKey is { Length: > 0 }) CryptographicOperations.ZeroMemory(verificationKey);
        }
    }

    public IReadOnlyList<VaultSnapshotInfo> GetSnapshots()
    {
        ThrowIfDisposed();
        EnsureOpen();
        return storageService.GetSnapshots();
    }

    public bool TryRestoreSnapshot(string snapshotId, string currentMasterPassword, out string errorMessage)
    {
        ThrowIfDisposed();
        EnsureOpen();
        errorMessage = string.Empty;
        byte[]? verificationKey = null;
        try
        {
            var config = storageService.LoadConfig();
            if (!TryVerifyCurrentMasterPassword(currentMasterPassword, config, out verificationKey))
            {
                errorMessage = "The Master Password is incorrect.";
                return false;
            }

            storageService.RestoreSnapshot(snapshotId);
            ClearSession();
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException
            or InvalidOperationException or CryptographicException or FormatException or System.Text.Json.JsonException)
        {
            errorMessage = "The selected snapshot could not be restored.";
            return false;
        }
        finally
        {
            if (verificationKey is { Length: > 0 }) CryptographicOperations.ZeroMemory(verificationKey);
        }
    }

    public IReadOnlyList<VaultItem> GetItems()
    {
        ThrowIfDisposed();
        EnsureOpen();

        return vaultData!.Items
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
            .Select(CloneForList)
            .ToList();
    }

    public VaultItem GetItemForEditing(Guid id, string totpCode)
    {
        ThrowIfDisposed();
        EnsureOpen();
        RequireSensitiveTotp(totpCode);
        return Clone(FindItem(id), includePassword: true);
    }

    public IReadOnlyList<PasswordHistoryEntry> GetPasswordHistory(Guid id, string totpCode)
    {
        ThrowIfDisposed();
        EnsureOpen();
        RequireSensitiveTotp(totpCode);
        var item = FindItem(id);
        return item.PasswordHistory
            .OrderByDescending(entry => entry.ChangedAt)
            .Select(entry => new PasswordHistoryEntry { Password = entry.Password, ChangedAt = entry.ChangedAt })
            .ToList();
    }

    public IReadOnlyList<VaultItem> GetDeletedItems()
    {
        ThrowIfDisposed();
        EnsureOpen();
        return vaultData!.Items.Where(item => item.IsDeleted)
            .OrderByDescending(item => item.DeletedAt)
            .Select(CloneForList)
            .ToList();
    }

    public void RestoreDeletedItem(Guid id)
    {
        ThrowIfDisposed();
        EnsureOpen();
        var item = FindAnyItem(id);
        if (!item.IsDeleted) throw new InvalidOperationException("The selected item is not in Trash.");
        item.IsDeleted = false;
        item.DeletedAt = null;
        item.UpdatedAt = utcNow();
        SaveVault();
    }

    public void PermanentlyDeleteItem(Guid id, string totpCode)
    {
        ThrowIfDisposed();
        EnsureOpen();
        RequireSensitiveTotp(totpCode);
        var item = FindAnyItem(id);
        if (!item.IsDeleted) throw new InvalidOperationException("Move the item to Trash before permanently deleting it.");
        vaultData!.Items.Remove(item);
        SaveVault();
    }

    public IReadOnlyList<VaultSecurityFinding> GetSecurityFindings(string totpCode)
    {
        ThrowIfDisposed();
        EnsureOpen();
        RequireSensitiveTotp(totpCode);
        var activePasswordItems = vaultData!.Items
            .Where(item => !item.IsDeleted && item.Type == VaultItemType.Password)
            .ToList();
        var reusedIds = activePasswordItems
            .Where(item => !string.IsNullOrEmpty(item.Password))
            .GroupBy(item => item.Password, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .SelectMany(group => group.Select(item => item.Id))
            .ToHashSet();
        var oldBefore = utcNow().AddDays(-365);
        var findings = new List<VaultSecurityFinding>();

        foreach (var item in activePasswordItems)
        {
            var strength = passwordGeneratorService.EstimatePasswordStrength(item.Password);
            if (item.Password.Length < 12 || strength.EstimatedEntropyBits < 60)
            {
                findings.Add(new(item.Id, item.Title, VaultSecurityFindingType.WeakPassword,
                    "Use a longer, randomly generated password."));
            }
            if (reusedIds.Contains(item.Id))
            {
                findings.Add(new(item.Id, item.Title, VaultSecurityFindingType.ReusedPassword,
                    "This password is also used by another active item."));
            }
            if (item.UpdatedAt < oldBefore)
            {
                findings.Add(new(item.Id, item.Title, VaultSecurityFindingType.OldPassword,
                    "This password has not been updated for more than one year."));
            }
        }

        return findings;
    }

    public string GetUsername(Guid id)
    {
        ThrowIfDisposed();
        EnsureOpen();
        return FindItem(id).Username;
    }

    public string GetPassword(Guid id, string totpCode)
    {
        ThrowIfDisposed();
        EnsureOpen();
        RequireSensitiveTotp(totpCode);
        var item = FindItem(id);
        if (item.Type != VaultItemType.Password)
        {
            throw new InvalidOperationException("This vault item stores recovery codes, not a password.");
        }

        return item.Password;
    }

    public IReadOnlyList<string> GetRecoveryCodes(Guid id, string totpCode)
    {
        ThrowIfDisposed();
        EnsureOpen();
        RequireSensitiveTotp(totpCode);

        var item = FindItem(id);
        if (item.Type != VaultItemType.RecoveryCodes)
        {
            throw new InvalidOperationException("This vault item does not store recovery codes.");
        }

        return item.RecoveryCodes.ToList();
    }

    public TotpCodeResult GetWebsiteTotpCode(Guid id, string totpCode)
    {
        ThrowIfDisposed();
        EnsureOpen();
        RequireSensitiveTotp(totpCode);
        var item = FindItem(id);
        if (item.Type != VaultItemType.Password || string.IsNullOrWhiteSpace(item.TotpSecretBase32))
        {
            throw new InvalidOperationException("This vault item does not contain a website TOTP secret.");
        }

        return totpService.GetCurrentCode(item.TotpSecretBase32, utcNow());
    }

    public string ExportBackupJson(string passphrase, string totpCode)
    {
        ThrowIfDisposed();
        EnsureOpen();
        RequireSensitiveTotp(totpCode);
        return backupService.CreateBackup(vaultData!.Items, passphrase, utcNow());
    }

    public VaultBackupImportPlan PreviewBackupImport(string backupJson, string passphrase, string totpCode)
    {
        ThrowIfDisposed();
        EnsureOpen();
        RequireSensitiveTotp(totpCode);

        var importedItems = backupService.ReadBackup(backupJson, passphrase);
        return backupService.CreateImportPlan(importedItems, vaultData!.Items);
    }

    public int ImportBackupJson(string backupJson, string passphrase, string totpCode)
    {
        ThrowIfDisposed();
        EnsureOpen();
        RequireSensitiveTotp(totpCode);

        var importedItems = backupService.ReadBackup(backupJson, passphrase);
        var plan = backupService.CreateImportPlan(importedItems, vaultData!.Items);
        var newIds = plan.Items
            .Where(item => item.Status == VaultBackupImportStatus.New)
            .Select(item => item.Id)
            .ToHashSet();

        if (newIds.Count == 0)
        {
            return 0;
        }

        var newItems = importedItems
            .Where(item => newIds.Contains(item.Id))
            .Select(item => Clone(item, includePassword: true))
            .ToList();

        vaultData.Items.AddRange(newItems);
        try
        {
            SaveVault();
            return newItems.Count;
        }
        catch
        {
            foreach (var newItem in newItems)
            {
                vaultData.Items.Remove(newItem);
            }

            throw;
        }
    }

    public VaultCsvImportPlan PreviewCsvImport(string csv, string totpCode)
    {
        ThrowIfDisposed();
        EnsureOpen();
        RequireSensitiveTotp(totpCode);
        var importedItems = csvImportService.Parse(csv);
        return csvImportService.CreateImportPlan(importedItems, vaultData!.Items);
    }

    public int ImportCsv(string csv, string totpCode)
    {
        ThrowIfDisposed();
        EnsureOpen();
        RequireSensitiveTotp(totpCode);
        var importedItems = csvImportService.Parse(csv);
        var newItems = csvImportService.SelectNewItems(importedItems, vaultData!.Items)
            .Select(item => Clone(item, includePassword: true))
            .ToList();
        if (newItems.Count == 0)
        {
            return 0;
        }

        var now = utcNow();
        foreach (var item in newItems)
        {
            item.Id = Guid.NewGuid();
            item.CreatedAt = now;
            item.UpdatedAt = now;
            ValidateVaultItem(item);
        }

        vaultData.Items.AddRange(newItems);
        try
        {
            SaveVault();
            return newItems.Count;
        }
        catch
        {
            foreach (var item in newItems) vaultData.Items.Remove(item);
            throw;
        }
    }

    public VaultItem AddItem(VaultItem item)
    {
        ThrowIfDisposed();
        EnsureOpen();
        ValidateVaultItem(item);

        var now = utcNow();
        var newItem = Clone(item, includePassword: true);
        newItem.Id = Guid.NewGuid();
        newItem.CreatedAt = now;
        newItem.UpdatedAt = now;

        vaultData!.Items.Add(newItem);
        SaveVault();
        return CloneForList(newItem);
    }

    public void UpdateItem(VaultItem item)
    {
        ThrowIfDisposed();
        EnsureOpen();
        ValidateVaultItem(item);

        var existing = FindItem(item.Id);
        if (existing.Type == VaultItemType.Password
            && !string.IsNullOrEmpty(existing.Password)
            && !string.Equals(existing.Password, item.Password, StringComparison.Ordinal))
        {
            existing.PasswordHistory.Insert(0, new PasswordHistoryEntry
            {
                Password = existing.Password,
                ChangedAt = utcNow()
            });
            existing.PasswordHistory = existing.PasswordHistory.Take(10).ToList();
        }
        existing.Title = item.Title.Trim();
        existing.Type = item.Type;
        existing.Username = item.Username.Trim();
        existing.Password = item.Type == VaultItemType.Password ? item.Password : string.Empty;
        existing.TotpSecretBase32 = item.Type == VaultItemType.Password ? item.TotpSecretBase32 : string.Empty;
        existing.RecoveryCodes = item.Type == VaultItemType.RecoveryCodes ? [.. item.RecoveryCodes] : [];
        existing.Url = item.Url.Trim();
        existing.HideUrl = item.HideUrl;
        existing.Notes = item.Notes;
        existing.HideNotes = item.HideNotes;
        existing.IsFavorite = item.IsFavorite;
        existing.Folder = item.Folder.Trim();
        existing.Tags = [.. item.Tags];
        existing.UpdatedAt = utcNow();

        SaveVault();
    }

    public void DeleteItem(Guid id)
    {
        ThrowIfDisposed();
        EnsureOpen();

        var item = FindItem(id);
        item.IsDeleted = true;
        item.DeletedAt = utcNow();
        item.UpdatedAt = item.DeletedAt.Value;
        SaveVault();
    }

    public void ClearSession()
    {
        if (encryptionKey is { Length: > 0 })
        {
            CryptographicOperations.ZeroMemory(encryptionKey);
        }

        encryptionKey = null;
        totpSecretBase32 = null;
        vaultData = null;
        isVaultOpen = false;
        sensitiveSessionExpiresAt = null;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        ClearSession();
        disposed = true;
    }

    private void SaveVault()
    {
        EnsureMasterPasswordUnlocked();
        storageService.SaveVaultPayload(encryptionService.EncryptObject(vaultData, encryptionKey!));
    }

    private void UpdateBackupHealth(
        DateTimeOffset? lastExternalBackupAt = null,
        DateTimeOffset? lastVerifiedBackupAt = null)
    {
        var config = storageService.LoadConfig();
        if (lastExternalBackupAt.HasValue) config.LastExternalBackupAt = lastExternalBackupAt;
        if (lastVerifiedBackupAt.HasValue) config.LastVerifiedBackupAt = lastVerifiedBackupAt;
        config.UpdatedAt = utcNow();
        storageService.SaveConfig(config);
    }

    private static string ReadBoundedBackupFile(string backupPath)
    {
        if (string.IsNullOrWhiteSpace(backupPath))
        {
            throw new ArgumentException("A backup file is required.", nameof(backupPath));
        }

        var info = new FileInfo(backupPath);
        if (!info.Exists)
        {
            throw new FileNotFoundException("The selected backup file was not found.", backupPath);
        }
        if (info.Length > VaultBackupService.MaxBackupJsonCharacters)
        {
            throw new InvalidDataException("The selected backup exceeds the 10 MB limit.");
        }

        return File.ReadAllText(backupPath);
    }

    private static void WriteExternalBackupFile(string destinationPath, string backupJson)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            throw new ArgumentException("A backup destination is required.", nameof(destinationPath));
        }

        var fullPath = Path.GetFullPath(destinationPath);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("The backup destination is invalid.", nameof(destinationPath));
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException("The backup destination folder does not exist.");
        }

        var tempPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(tempPath, backupJson);
            File.Move(tempPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    private void SaveTrustedUnlockToken(AppConfig config)
    {
        if (encryptionKey is null || string.IsNullOrWhiteSpace(config.EncryptedTotpSecret))
        {
            return;
        }

        try
        {
            var now = utcNow();
            var token = trustedUnlockTokenService.CreateToken(
                encryptionKey,
                ComputeConfigFingerprint(config),
                now,
                now.AddDays(1));
            storageService.SaveTrustedUnlockToken(token);
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or CryptographicException
            or InvalidOperationException
            or NotSupportedException
            or Win32Exception)
        {
            TryDeleteTrustedUnlockToken();
        }
    }

    private void TryDeleteTrustedUnlockToken()
    {
        try
        {
            storageService.DeleteTrustedUnlockToken();
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
        }
    }

    private static byte[] ComputeConfigFingerprint(AppConfig config)
    {
        var fingerprintInput = string.Join('\n',
            config.Version.ToString(System.Globalization.CultureInfo.InvariantCulture),
            config.KdfAlgorithm,
            config.KdfIterations.ToString(System.Globalization.CultureInfo.InvariantCulture),
            config.KdfMemorySizeKb.ToString(System.Globalization.CultureInfo.InvariantCulture),
            config.KdfParallelism.ToString(System.Globalization.CultureInfo.InvariantCulture),
            config.KeySizeBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
            config.SaltBase64,
            config.EncryptedTotpSecret);

        return SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintInput));
    }

    private VaultItem FindItem(Guid id)
    {
        return vaultData!.Items.FirstOrDefault(item => item.Id == id && !item.IsDeleted)
            ?? throw new InvalidOperationException("The selected vault item no longer exists.");
    }

    private VaultItem FindAnyItem(Guid id) => vaultData!.Items.FirstOrDefault(item => item.Id == id)
        ?? throw new InvalidOperationException("The selected vault item no longer exists.");

    private bool TryVerifyCurrentMasterPassword(string masterPassword, AppConfig config, out byte[] verificationKey)
    {
        verificationKey = [];
        if (!masterPasswordService.TryUnlockConfig(masterPassword, config, out verificationKey, out _)) return false;
        if (encryptionKey is not { Length: > 0 }
            || verificationKey.Length != encryptionKey.Length
            || !CryptographicOperations.FixedTimeEquals(verificationKey, encryptionKey))
        {
            CryptographicOperations.ZeroMemory(verificationKey);
            verificationKey = [];
            return false;
        }
        return true;
    }

    private void PurgeExpiredTrash()
    {
        var cutoff = utcNow().AddDays(-30);
        var removed = vaultData!.Items.RemoveAll(item => item.IsDeleted && item.DeletedAt is { } deletedAt && deletedAt <= cutoff);
        if (removed > 0) SaveVault();
    }

    private void RequireSensitiveTotp(string code)
    {
        if (!VerifyTotpForSensitiveAction(code))
        {
            throw new UnauthorizedAccessException("Authenticator verification failed.");
        }
    }

    private void EnsureMasterPasswordUnlocked()
    {
        if (encryptionKey is null || vaultData is null)
        {
            throw new InvalidOperationException("The vault is not unlocked with the Master Password.");
        }
    }

    private void EnsureOpen()
    {
        EnsureMasterPasswordUnlocked();

        if (!isVaultOpen)
        {
            throw new InvalidOperationException("The vault requires Authenticator verification before it can be opened.");
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    private void ValidateVaultItem(VaultItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (string.IsNullOrWhiteSpace(item.Title))
        {
            throw new ArgumentException("Title is required.", nameof(item));
        }

        if (!Enum.IsDefined(item.Type))
        {
            throw new ArgumentException("The selected vault item type is not supported.", nameof(item));
        }

        item.RecoveryCodes ??= [];
        item.Tags ??= [];
        item.PasswordHistory ??= [];
        item.Tags = item.Tags
            .Select(tag => tag.Trim())
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        item.Folder = item.Folder?.Trim() ?? string.Empty;
        if (item.Type == VaultItemType.Password && string.IsNullOrWhiteSpace(item.Password))
        {
            throw new ArgumentException("Password is required.", nameof(item));
        }

        if (item.Type == VaultItemType.Password && item.RecoveryCodes.Count != 0)
        {
            throw new ArgumentException("A password item cannot contain recovery codes.", nameof(item));
        }

        if (item.Type == VaultItemType.Password && !string.IsNullOrWhiteSpace(item.TotpSecretBase32))
        {
            if (!totpService.TryNormalizeWebsiteSecret(item.TotpSecretBase32, out var normalizedSecret))
            {
                throw new ArgumentException("The website TOTP secret is invalid.", nameof(item));
            }
            item.TotpSecretBase32 = normalizedSecret;
        }

        if (item.Type == VaultItemType.RecoveryCodes
            && (!string.IsNullOrEmpty(item.Password)
                || !string.IsNullOrEmpty(item.TotpSecretBase32)
                || item.RecoveryCodes.Count < 2))
        {
            throw new ArgumentException("A recovery-code item requires at least two codes and cannot contain a password.", nameof(item));
        }

        VaultBackupService.ValidateItems([item]);
    }

    private static VaultItem Clone(VaultItem item, bool includePassword)
    {
        return new VaultItem
        {
            Id = item.Id,
            Title = item.Title,
            Type = item.Type,
            Username = item.Username,
            Password = includePassword ? item.Password : string.Empty,
            TotpSecretBase32 = includePassword ? item.TotpSecretBase32 : string.Empty,
            RecoveryCodes = includePassword ? [.. item.RecoveryCodes] : [],
            Url = item.Url,
            HideUrl = item.HideUrl,
            Notes = item.Notes,
            HideNotes = item.HideNotes,
            IsFavorite = item.IsFavorite,
            Folder = item.Folder,
            Tags = [.. item.Tags],
            PasswordHistory = includePassword
                ? item.PasswordHistory.Select(entry => new PasswordHistoryEntry
                {
                    Password = entry.Password,
                    ChangedAt = entry.ChangedAt
                }).ToList()
                : [],
            IsDeleted = item.IsDeleted,
            DeletedAt = item.DeletedAt,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        };
    }

    private static VaultItem CloneForList(VaultItem item)
    {
        var clone = Clone(item, includePassword: false);
        clone.RecoveryCodeCount = item.RecoveryCodes.Count;
        clone.HasTotp = !string.IsNullOrWhiteSpace(item.TotpSecretBase32);
        if (clone.HideUrl)
        {
            clone.Url = string.Empty;
        }

        if (clone.HideNotes)
        {
            clone.Notes = string.Empty;
        }

        return clone;
    }

    private static void NormalizeItems(IEnumerable<VaultItem> items)
    {
        foreach (var item in items)
        {
            item.RecoveryCodes ??= [];
            item.Tags ??= [];
            item.PasswordHistory ??= [];
        }
    }
}
