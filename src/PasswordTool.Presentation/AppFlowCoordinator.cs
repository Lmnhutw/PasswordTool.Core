using PasswordTool.Core.Models;
using PasswordTool.Core.Services;

namespace PasswordTool.Presentation;

/// <summary>Owns app-flow state while keeping VaultService access serialized.</summary>
public sealed class AppFlowCoordinator
{
    private readonly VaultService vaultService;
    private readonly IVaultOperationRunner operations;
    private readonly TotpService totpService;
    private readonly bool hasPartialStorage;

    public AppFlowCoordinator(VaultService vaultService, IVaultOperationRunner operations, TotpService totpService)
    {
        this.vaultService = vaultService;
        this.operations = operations;
        this.totpService = totpService;
        hasPartialStorage = vaultService.HasPartialStorage;
        FlowState = vaultService.IsVaultUnlocked ? AppFlowState.Unlocked : ResolveInitialState(vaultService.IsInitialized, hasPartialStorage);
    }

    public AppFlowState FlowState { get; private set; }
    public bool HasPartialStorage => hasPartialStorage;
    public bool IsSignedIn => vaultService.IsSignInSessionActive;
    private long lifecycleVersion;
    public long LifecycleVersion => Volatile.Read(ref lifecycleVersion);
    public bool IsCurrentUnlock(long version) => version == LifecycleVersion && IsSignedIn && FlowState == AppFlowState.Unlocked;

    public static AppFlowState ResolveInitialState(bool isInitialized, bool hasPartialStorage) =>
        hasPartialStorage ? AppFlowState.Recover : isInitialized ? AppFlowState.Unlock : AppFlowState.FirstLaunch;

    public Task<bool> ValidateMasterPasswordAsync(string masterPassword, CancellationToken cancellationToken = default) =>
        operations.RunAsync(() => vaultService.ValidateMasterPassword(masterPassword), cancellationToken);

    public async Task<VaultUnlockResult> UnlockAsync(string masterPassword, string totpCode, CancellationToken cancellationToken = default)
    {
        var version = Volatile.Read(ref lifecycleVersion);
        var result = await operations.RunAsync(() =>
        {
            if (version != Volatile.Read(ref lifecycleVersion))
                return new VaultUnlockResult(VaultUnlockStatus.Failed, "Unlock cancelled.");
            var signedIn = vaultService.IsSignInSessionActive;
            var unlocked = vaultService.UnlockWithMasterPassword(masterPassword);
            if (!unlocked.Success) return unlocked;

            var verified = false;
            try
            {
                verified = signedIn ? vaultService.IsSignInSessionActive : vaultService.VerifyTotpForSession(totpCode);
                verified &= version == Volatile.Read(ref lifecycleVersion);
                return verified ? unlocked : new VaultUnlockResult(VaultUnlockStatus.Failed, "Invalid Google Authenticator code.");
            }
            finally
            {
                if (!verified) vaultService.LockVault();
            }
        }, cancellationToken).ConfigureAwait(false);
        if (result.Success && version == Volatile.Read(ref lifecycleVersion)) FlowState = AppFlowState.Unlocked;
        else if (result.Success) return new VaultUnlockResult(VaultUnlockStatus.Failed, "Unlock cancelled.");
        return result;
    }

    public void BeginNewVault() => FlowState = AppFlowState.CreateMasterPassword;

    public void BeginRecovery() => FlowState = AppFlowState.Recover;

    public void ReturnToFirstLaunch() => FlowState = AppFlowState.FirstLaunch;

    public AuthenticatorSetup PrepareAuthenticator(string masterPassword, string confirmation)
    {
        MasterPasswordService.ValidateNewMasterPassword(masterPassword);
        if (!string.Equals(masterPassword, confirmation, StringComparison.Ordinal))
            throw new ArgumentException("The Master Password values do not match.", nameof(confirmation));

        var secret = totpService.GenerateSecret();
        FlowState = AppFlowState.SetupAuthenticator;
        return new AuthenticatorSetup(
            secret,
            totpService.CreateOtpAuthUri(secret, "PasswordTool", Environment.UserName));
    }

    public AuthenticatorSetup CreateAuthenticatorSetup()
    {
        var secret = totpService.GenerateSecret();
        return new AuthenticatorSetup(
            secret,
            totpService.CreateOtpAuthUri(secret, "PasswordTool", Environment.UserName));
    }

    public Task<VaultBackupInspection> InspectRecoveryBackupAsync(
        string backupPath,
        string passphrase,
        CancellationToken cancellationToken = default) =>
        operations.RunAsync(
            () => vaultService.InspectBackupJson(ReadBoundedBackupFile(backupPath), passphrase),
            cancellationToken);

    public async Task CompleteNewVaultAsync(
        string masterPassword,
        AuthenticatorSetup setup,
        string confirmationCode,
        CancellationToken cancellationToken = default)
    {
        await operations.RunAsync(
            () => vaultService.InitializeNewVault(masterPassword, setup.SecretBase32, confirmationCode),
            cancellationToken).ConfigureAwait(false);
        FlowState = AppFlowState.Unlocked;
    }

    public async Task CompleteRecoveryAsync(
        string backupPath,
        string backupPassphrase,
        string masterPassword,
        AuthenticatorSetup setup,
        string confirmationCode,
        CancellationToken cancellationToken = default)
    {
        await operations.RunAsync(() => vaultService.RecoverFromBackup(new VaultRecoveryRequest
        {
            BackupJson = ReadBoundedBackupFile(backupPath),
            BackupPassphrase = backupPassphrase,
            NewMasterPassword = masterPassword,
            NewTotpSecretBase32 = setup.SecretBase32,
            TotpConfirmationCode = confirmationCode
        }), cancellationToken).ConfigureAwait(false);
        FlowState = AppFlowState.Unlocked;
    }

    public async Task LockAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref lifecycleVersion);
        FlowState = AppFlowState.Unlock;
        await operations.RunAsync(() =>
        {
            if (vaultService.IsSignInSessionActive) vaultService.LockVault();
            else vaultService.ClearSession();
        }, cancellationToken).ConfigureAwait(false);
        FlowState = AppFlowState.Unlock;
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref lifecycleVersion);
        FlowState = AppFlowState.Unlock;
        await operations.RunAsync(vaultService.ClearSession, cancellationToken).ConfigureAwait(false);
        FlowState = AppFlowState.Unlock;
    }

    public Task<IReadOnlyList<VaultItemListItem>> GetListItemsAsync(CancellationToken cancellationToken = default) =>
        RunVaultAsync(() => (IReadOnlyList<VaultItemListItem>)vaultService.GetItems().Select(VaultItemListItem.FromVaultItem).ToList(), cancellationToken);

    public Task<IReadOnlyList<VaultGroup>> GetGroupsAsync(CancellationToken cancellationToken = default) =>
        RunVaultAsync(vaultService.GetGroups, cancellationToken);

    public Task<VaultGroup> AddGroupAsync(string name, string? accentColor = null, CancellationToken cancellationToken = default) =>
        RunVaultAsync(() => vaultService.AddGroup(name, accentColor), cancellationToken);

    public Task UpdateGroupAsync(Guid id, string name, string? accentColor, CancellationToken cancellationToken = default) =>
        RunVaultAsync(() => vaultService.UpdateGroup(id, name, accentColor), cancellationToken);

    public Task DeleteGroupAsync(Guid id, string confirmation, string totpCode, CancellationToken cancellationToken = default) =>
        RunVaultAsync(() => vaultService.DeleteGroup(id, confirmation, totpCode), cancellationToken);

    public Task<TimeSpan> GetInactivityTimeoutAsync(CancellationToken cancellationToken = default) =>
        RunVaultAsync(
            () => TimeSpan.FromMinutes(vaultService.SecuritySettings.InactivityLockTimeoutMinutes),
            cancellationToken);

    public Task<TimeSpan> GetVaultOpenDurationAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(TimeSpan.FromMinutes(VaultSecuritySettings.MaximumSessionDurationMinutes));

    public Task<VaultItem> GetItemForEditingAsync(
        Guid id,
        string totpCode,
        CancellationToken cancellationToken = default) =>
        RunVaultAsync(() => vaultService.GetItemForEditing(id, totpCode), cancellationToken);

    public Task AddItemAsync(VaultItem item, CancellationToken cancellationToken = default) =>
        RunVaultAsync(() => vaultService.AddItem(item), cancellationToken);

    public Task UpdateItemAsync(VaultItem item, CancellationToken cancellationToken = default) =>
        RunVaultAsync(() => vaultService.UpdateItem(item), cancellationToken);

    public Task DeleteItemAsync(Guid id, CancellationToken cancellationToken = default) =>
        RunVaultAsync(() => vaultService.DeleteItem(id), cancellationToken);

    public Task<string> GetUsernameAsync(Guid id, CancellationToken cancellationToken = default) =>
        RunVaultAsync(() => vaultService.GetUsername(id), cancellationToken);

    public Task<string> GetPasswordAsync(
        Guid id,
        string totpCode,
        CancellationToken cancellationToken = default) =>
        RunVaultAsync(() => vaultService.GetPassword(id, totpCode), cancellationToken);

    public Task<IReadOnlyList<string>> GetRecoveryCodesAsync(
        Guid id,
        string totpCode,
        CancellationToken cancellationToken = default) =>
        RunVaultAsync(() => vaultService.GetRecoveryCodes(id, totpCode), cancellationToken);

    public Task<TotpCodeResult> GetWebsiteTotpCodeAsync(
        Guid id,
        string totpCode,
        CancellationToken cancellationToken = default) =>
        RunVaultAsync(() => vaultService.GetWebsiteTotpCode(id, totpCode), cancellationToken);

    public Task<IReadOnlyList<PasswordHistoryEntry>> GetPasswordHistoryAsync(
        Guid id,
        string totpCode,
        CancellationToken cancellationToken = default) =>
        RunVaultAsync(() => vaultService.GetPasswordHistory(id, totpCode), cancellationToken);

    public Task<IReadOnlyList<TrashItemListItem>> GetDeletedItemsAsync(CancellationToken cancellationToken = default) =>
        RunVaultAsync(
            () => (IReadOnlyList<TrashItemListItem>)vaultService.GetDeletedItems().Select(TrashItemListItem.FromVaultItem).ToList(),
            cancellationToken);

    public Task RestoreDeletedItemAsync(Guid id, CancellationToken cancellationToken = default) =>
        RunVaultAsync(() => vaultService.RestoreDeletedItem(id), cancellationToken);

    public Task PermanentlyDeleteItemAsync(
        Guid id,
        string totpCode,
        CancellationToken cancellationToken = default) =>
        RunVaultAsync(() => vaultService.PermanentlyDeleteItem(id, totpCode), cancellationToken);

    public Task<SettingsSnapshot> GetSettingsAsync(CancellationToken cancellationToken = default) =>
        RunVaultAsync(() =>
        {
            var security = vaultService.SecuritySettings;
            return new SettingsSnapshot(
                security.InactivityLockTimeoutMinutes,
                vaultService.NeedsKdfUpgrade,
                vaultService.LastExternalBackupAt,
                vaultService.LastVerifiedBackupAt);
        }, cancellationToken);

    public Task<OperationResult> UpdateSettingsAsync(
        string masterPassword,
        int inactivityTimeoutMinutes,
        CancellationToken cancellationToken = default) =>
        RunVaultAsync(() =>
        {
            var success = vaultService.TryUpdateSettings(
                masterPassword,
                VaultLoginMode.Hybrid,
                new VaultSecuritySettings(
                    inactivityTimeoutMinutes,
                    VaultSecuritySettings.DefaultSensitiveActionTimeoutMinutes,
                    VaultSecuritySettings.MaximumSessionDurationMinutes),
                out var message);
            return new OperationResult(success, message);
        }, cancellationToken);

    public Task<OperationResult> ChangeMasterPasswordAsync(
        string currentMasterPassword,
        string newMasterPassword,
        CancellationToken cancellationToken = default) =>
        RunVaultAsync(() =>
        {
            var success = vaultService.TryChangeMasterPassword(currentMasterPassword, newMasterPassword, out var message);
            return new OperationResult(success, message);
        }, cancellationToken);

    public Task<OperationResult> UpgradeKdfAsync(
        string masterPassword,
        CancellationToken cancellationToken = default) =>
        RunVaultAsync(() =>
        {
            var success = vaultService.TryUpgradeKdf(masterPassword, out var message);
            return new OperationResult(success, message);
        }, cancellationToken);

    public Task<OperationResult> ResetAuthenticatorAsync(
        string masterPassword,
        AuthenticatorSetup setup,
        string confirmationCode,
        CancellationToken cancellationToken = default) =>
        RunVaultAsync(() =>
        {
            var success = vaultService.TryResetAuthenticator(
                masterPassword,
                setup.SecretBase32,
                confirmationCode,
                out var message);
            return new OperationResult(success, message);
        }, cancellationToken);

    public Task CreateExternalBackupAsync(
        string destinationPath,
        string passphrase,
        string totpCode,
        CancellationToken cancellationToken = default) =>
        RunVaultAsync(() => vaultService.CreateExternalBackupFile(destinationPath, passphrase, totpCode), cancellationToken);

    public Task<VaultBackupInspection> VerifyExternalBackupAsync(
        string path,
        string passphrase,
        CancellationToken cancellationToken = default) =>
        RunVaultAsync(() => vaultService.VerifyExternalBackupFile(path, passphrase), cancellationToken);

    public Task<VaultBackupImportPlan> PreviewBackupImportAsync(
        string path,
        string passphrase,
        string totpCode,
        CancellationToken cancellationToken = default) =>
        RunVaultAsync(
            () => vaultService.PreviewBackupImport(ReadBoundedFile(path, VaultBackupService.MaxBackupJsonCharacters), passphrase, totpCode),
            cancellationToken);

    public Task<int> ImportBackupAsync(
        string path,
        string passphrase,
        string totpCode,
        CancellationToken cancellationToken = default) =>
        RunVaultAsync(
            () => vaultService.ImportBackupJson(ReadBoundedFile(path, VaultBackupService.MaxBackupJsonCharacters), passphrase, totpCode),
            cancellationToken);

    public Task<VaultCsvImportPlan> PreviewCsvImportAsync(
        string path,
        string totpCode,
        CancellationToken cancellationToken = default) =>
        RunVaultAsync(
            () => vaultService.PreviewCsvImport(ReadBoundedFile(path, VaultCsvImportService.MaxCsvCharacters), totpCode),
            cancellationToken);

    public Task<int> ImportCsvAsync(
        string path,
        string totpCode,
        CancellationToken cancellationToken = default) =>
        RunVaultAsync(
            () => vaultService.ImportCsv(ReadBoundedFile(path, VaultCsvImportService.MaxCsvCharacters), totpCode),
            cancellationToken);

    public Task<IReadOnlyList<VaultSnapshotInfo>> GetSnapshotsAsync(CancellationToken cancellationToken = default) =>
        RunVaultAsync(() => vaultService.GetSnapshots(), cancellationToken);

    public Task<IReadOnlyList<VaultSecurityFinding>> GetSecurityFindingsAsync(
        string totpCode,
        CancellationToken cancellationToken = default) =>
        RunVaultAsync(() => vaultService.GetSecurityFindings(totpCode), cancellationToken);

    public async Task<OperationResult> RestoreSnapshotAsync(
        string snapshotId,
        string masterPassword,
        CancellationToken cancellationToken = default)
    {
        var result = await operations.RunAsync(() =>
        {
            var success = vaultService.TryRestoreSnapshot(snapshotId, masterPassword, out var message);
            return new OperationResult(success, message);
        }, cancellationToken).ConfigureAwait(false);
        if (result.Success) FlowState = AppFlowState.Unlock;
        return result;
    }

    private static string ReadBoundedBackupFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A backup file is required.", nameof(path));
        var info = new FileInfo(path);
        if (info.Length > VaultBackupService.MaxBackupJsonCharacters)
            throw new InvalidDataException("The selected backup exceeds the 10 MB limit.");
        return File.ReadAllText(path);
    }

    private async Task<T> RunVaultAsync<T>(Func<T> operation, CancellationToken cancellationToken)
    {
        var version = LifecycleVersion;
        var result = await operations.RunAsync(() =>
        {
            if (!IsCurrentUnlock(version)) throw new OperationCanceledException("The vault was locked or the sign-in session expired.");
            return operation();
        }, cancellationToken).ConfigureAwait(false);
        if (!IsCurrentUnlock(version)) throw new OperationCanceledException("The vault was locked or the sign-in session expired.");
        return result;
    }

    private Task RunVaultAsync(Action operation, CancellationToken cancellationToken) =>
        RunVaultAsync(() => { operation(); return true; }, cancellationToken);

    private static string ReadBoundedFile(string path, int maximumCharacters)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A file is required.", nameof(path));
        var info = new FileInfo(path);
        if (info.Length > maximumCharacters) throw new InvalidDataException("The selected file exceeds the 10 MB limit.");
        var content = File.ReadAllText(path);
        if (content.Length > maximumCharacters) throw new InvalidDataException("The selected file exceeds the 10 MB limit.");
        return content;
    }
}
