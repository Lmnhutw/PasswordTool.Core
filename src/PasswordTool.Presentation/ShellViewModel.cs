using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordTool.Core.Models;

namespace PasswordTool.Presentation;

public sealed partial class ShellViewModel : ObservableObject
{
    private readonly AppFlowCoordinator flow;
    private readonly INavigationService navigation;
    private readonly ISensitiveClipboardService clipboard;
    private readonly IFilePickerService filePicker;
    private readonly IUserErrorMapper errorMapper;
    private readonly IUserDialogService dialogs;
    private bool backupReminderOffered;

    public ShellViewModel(
        AppFlowCoordinator flow,
        VaultWorkspaceViewModel vault,
        HashToolViewModel hashTool,
        SettingsViewModel settings,
        BackupViewModel backup,
        SecurityCheckViewModel securityCheck,
        TrashViewModel trash,
        INavigationService navigation,
        ISensitiveClipboardService clipboard,
        IFilePickerService filePicker,
        IUserErrorMapper errorMapper,
        IUserDialogService dialogs)
    {
        this.flow = flow;
        this.navigation = navigation;
        this.clipboard = clipboard;
        this.filePicker = filePicker;
        this.errorMapper = errorMapper;
        this.dialogs = dialogs;
        Vault = vault;
        HashTool = hashTool;
        Settings = settings;
        Backup = backup;
        SecurityCheck = securityCheck;
        Trash = trash;
        FlowState = flow.FlowState;
        CurrentRoute = navigation.CurrentRoute;
    }

    public VaultWorkspaceViewModel Vault { get; }
    public HashToolViewModel HashTool { get; }
    public SettingsViewModel Settings { get; }
    public BackupViewModel Backup { get; }
    public SecurityCheckViewModel SecurityCheck { get; }
    public TrashViewModel Trash { get; }

    [ObservableProperty] public partial AppFlowState FlowState { get; set; }
    [ObservableProperty] public partial AppRoute CurrentRoute { get; set; } = AppRoute.Vault;
    [ObservableProperty] public partial string StatusMessage { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsStatusOpen { get; set; }
    [ObservableProperty] public partial TimeSpan InactivityTimeout { get; set; } = TimeSpan.FromMinutes(1);
    [ObservableProperty] public partial TimeSpan VaultOpenDuration { get; set; } = TimeSpan.FromHours(5);
    [ObservableProperty] public partial bool IsRecovering { get; set; }
    [ObservableProperty] public partial bool IsBackupReminderOpen { get; set; }
    [ObservableProperty] public partial string RecoveryPath { get; set; } = string.Empty;
    [ObservableProperty] public partial string RecoverySummary { get; set; } = string.Empty;
    [ObservableProperty] public partial AuthenticatorSetup? PendingAuthenticatorSetup { get; set; }

    public bool IsUnlocked => FlowState == AppFlowState.Unlocked;
    public bool IsSignedIn => flow.IsSignedIn;
    public long LifecycleVersion => flow.LifecycleVersion;
    public bool IsCurrentUnlock(long version) => flow.IsCurrentUnlock(version) && IsUnlocked;
    public bool HasPartialStorage => flow.HasPartialStorage;
    public bool IsVaultRoute => CurrentRoute == AppRoute.Vault;
    public bool IsHashToolRoute => CurrentRoute == AppRoute.HashTool;

    public async Task<bool> ValidateMasterPasswordAsync(string masterPassword)
    {
        IsStatusOpen = false;
        try
        {
            if (await flow.ValidateMasterPasswordAsync(masterPassword)) return true;
            StatusMessage = "The Master Password is incorrect. Please try again.";
            IsStatusOpen = true;
        }
        catch (Exception exception) { ShowMappedError(exception); }
        return false;
    }

    public async Task UnlockAsync(string masterPassword, string totpCode)
    {
        IsStatusOpen = false;
        var result = await flow.UnlockAsync(masterPassword, totpCode);
        FlowState = flow.FlowState;
        if (!result.Success)
        {
            StatusMessage = result.Message;
            IsStatusOpen = true;
            return;
        }

        try { await CompleteUnlockAsync(result); }
        catch (OperationCanceledException) { }
    }

    public void BeginNewVault()
    {
        IsRecovering = false;
        flow.BeginNewVault();
        FlowState = flow.FlowState;
        ClearAuthenticationStatus();
    }

    public void BeginRecovery()
    {
        IsRecovering = true;
        flow.BeginRecovery();
        FlowState = flow.FlowState;
        ClearAuthenticationStatus();
    }

    public async Task SelectRecoveryFileAsync()
    {
        var path = await filePicker.PickOpenPathAsync();
        if (!string.IsNullOrWhiteSpace(path)) RecoveryPath = path;
    }

    public async Task InspectRecoveryAsync(string passphrase)
    {
        try
        {
            var inspection = await flow.InspectRecoveryBackupAsync(RecoveryPath, passphrase);
            RecoverySummary = FormatInspection(inspection);
            ClearAuthenticationStatus();
        }
        catch (Exception exception)
        {
            ShowMappedError(exception);
        }
    }

    public void ContinueRecovery()
    {
        if (string.IsNullOrWhiteSpace(RecoverySummary)) return;
        flow.BeginNewVault();
        FlowState = flow.FlowState;
    }

    public void PrepareAuthenticator(string masterPassword, string confirmation)
    {
        try
        {
            PendingAuthenticatorSetup = flow.PrepareAuthenticator(masterPassword, confirmation);
            FlowState = flow.FlowState;
            ClearAuthenticationStatus();
        }
        catch (Exception exception)
        {
            ShowMappedError(exception);
        }
    }

    public async Task CompleteAuthenticatorSetupAsync(
        string masterPassword,
        string backupPassphrase,
        string confirmationCode)
    {
        if (PendingAuthenticatorSetup is null) return;
        try
        {
            if (IsRecovering)
            {
                await flow.CompleteRecoveryAsync(
                    RecoveryPath,
                    backupPassphrase,
                    masterPassword,
                    PendingAuthenticatorSetup,
                    confirmationCode);
            }
            else
            {
                await flow.CompleteNewVaultAsync(masterPassword, PendingAuthenticatorSetup, confirmationCode);
            }

            FlowState = flow.FlowState;
            PendingAuthenticatorSetup = null;
            RecoveryPath = string.Empty;
            RecoverySummary = string.Empty;
            await CompleteUnlockAsync(new VaultUnlockResult(VaultUnlockStatus.Unlocked));
        }
        catch (Exception exception)
        {
            ShowMappedError(exception);
        }
    }

    public void CancelFirstLaunchStep()
    {
        PendingAuthenticatorSetup = null;
        RecoveryPath = string.Empty;
        RecoverySummary = string.Empty;
        IsRecovering = false;
        flow.ReturnToFirstLaunch();
        FlowState = flow.FlowState;
        ClearAuthenticationStatus();
    }

    [RelayCommand]
    private async Task LockAsync()
    {
        IsBackupReminderOpen = false;
        FlowState = AppFlowState.Unlock;
        Vault.Clear();
        Trash.Clear();
        SecurityCheck.Clear();
        Backup.Clear();
        Settings.Clear();
        await flow.LockAsync();
        Vault.Clear();
        await clipboard.ClearOwnedValueAsync();
        navigation.ResetForLock();
        FlowState = flow.FlowState;
        CurrentRoute = navigation.CurrentRoute;
        StatusMessage = string.Empty;
        IsStatusOpen = false;
        PendingAuthenticatorSetup = null;
        OnPropertyChanged(nameof(IsSignedIn));
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await flow.LogoutAsync();
        await LockAsync();
    }

    public void Navigate(AppRoute route)
    {
        if (!IsUnlocked || !IsSignedIn) return;
        navigation.Navigate(route);
        CurrentRoute = navigation.CurrentRoute;
        OnPropertyChanged(nameof(IsVaultRoute));
        OnPropertyChanged(nameof(IsHashToolRoute));
    }

    public void BeginAddItem() => Navigate(AppRoute.ItemEditor);

    public async Task<VaultItem?> GetSelectedItemForEditingAsync()
    {
        if (Vault.SelectedItem is not { } selected) return null;
        return await GetItemForEditingAsync(selected.Id);
    }

    public async Task<VaultItem?> GetItemForEditingAsync(Guid itemId)
    {
        var version = LifecycleVersion;
        try
        {
            var item = await flow.GetItemForEditingAsync(itemId, string.Empty);
            return IsCurrentUnlock(version) ? item : null;
        }
        catch (Exception exception)
        {
            ShowMappedError(exception);
            return null;
        }
    }

    public async Task<bool> SaveItemAsync(VaultItemEditorInput input)
    {
        var version = LifecycleVersion;
        try
        {
            var item = input.ToVaultItem();
            if (input.Id is null) await flow.AddItemAsync(item);
            else await flow.UpdateItemAsync(item);
            await Vault.RefreshAsync();
            if (!IsCurrentUnlock(version)) return false;
            Navigate(AppRoute.Vault);
            ClearAuthenticationStatus();
            if (input.Id is null && !backupReminderOffered)
            {
                try
                {
                    if ((await flow.GetListItemsAsync()).Count == 1
                        && (await flow.GetDeletedItemsAsync()).Count == 0)
                    {
                        backupReminderOffered = true;
                        IsBackupReminderOpen = (await flow.GetSettingsAsync()).LastExternalBackupAt is null;
                    }
                }
                catch { /* Saving the item succeeded; a reminder must not turn it into a failure. */ }
            }
            return true;
        }
        catch (Exception exception)
        {
            ShowMappedError(exception);
            return false;
        }
    }

    public async Task<VaultGroup?> CreateGroupAsync(string name, string? accentColor = null)
    {
        try
        {
            var group = await flow.AddGroupAsync(name, accentColor);
            await Vault.RefreshAsync();
            return group;
        }
        catch (Exception exception) { ShowMappedError(exception); return null; }
    }

    public async Task<bool> UpdateGroupAsync(Guid id, string name, string? accentColor)
    {
        try { await flow.UpdateGroupAsync(id, name, accentColor); await Vault.RefreshAsync(); return true; }
        catch (Exception exception) { ShowMappedError(exception); return false; }
    }

    public async Task DeleteGroupAsync(Guid id, string name)
    {
        var confirmation = await dialogs.ConfirmGroupDeletionAsync(name);
        if (confirmation is null) return;
        try { await flow.DeleteGroupAsync(id, confirmation.Value.Confirmation, confirmation.Value.TotpCode); await Vault.RefreshAsync(); }
        catch (UnauthorizedAccessException) { await dialogs.ShowErrorAsync("Group was not deleted", "Incorrect authenticator code. Please try again."); }
        catch (Exception exception) { ShowMappedError(exception); }
    }

    public async Task DeleteItemAsync(Guid itemId)
    {
        var title = Vault.Items.FirstOrDefault(item => item.Id == itemId)?.Title;
        if (title is null || !await dialogs.ConfirmAsync("Move to Trash", $"Move '{title}' to Trash?", "Move to Trash")) return;
        try
        {
            await flow.DeleteItemAsync(itemId);
            await Vault.RefreshAsync();
            ClearAuthenticationStatus();
        }
        catch (Exception exception)
        {
            ShowMappedError(exception);
        }
    }

    public async Task CopyUsernameAsync(Guid itemId)
    {
        var version = LifecycleVersion;
        try
        {
            var value = await flow.GetUsernameAsync(itemId);
            if (IsCurrentUnlock(version)) await clipboard.CopyAsync(value);
        }
        catch (Exception exception)
        {
            ShowMappedError(exception);
        }
    }

    public async Task RevealPasswordAsync(Guid itemId)
    {
        var version = LifecycleVersion;
        try
        {
            var value = await flow.GetPasswordAsync(itemId, string.Empty);
            if (IsCurrentUnlock(version)) await dialogs.ShowSecretAsync("Password", value, multiline: false);
        }
        catch (Exception exception)
        {
            ShowMappedError(exception);
        }
    }

    public async Task RevealRecoveryCodesAsync(Guid itemId)
    {
        var version = LifecycleVersion;
        try
        {
            var codes = await flow.GetRecoveryCodesAsync(itemId, string.Empty);
            if (!IsCurrentUnlock(version)) return;
            await dialogs.ShowSecretAsync("Recovery codes", string.Join(Environment.NewLine, codes), multiline: true);
        }
        catch (Exception exception)
        {
            ShowMappedError(exception);
        }
    }

    public async Task RevealNotesAsync(Guid itemId)
    {
        var version = LifecycleVersion;
        var item = Vault.Items.FirstOrDefault(candidate => candidate.Id == itemId);
        if (item is null || !item.HasNotes) return;

        var notes = item.Notes;
        if (item.HideNotes)
        {
            var fullItem = await GetItemForEditingAsync(itemId);
            if (fullItem is null) return;
            notes = fullItem.Notes;
        }

        try
        {
            if (!IsCurrentUnlock(version)) return;
            await dialogs.ShowSecretAsync("Notes", notes, multiline: true);
        }
        catch (Exception exception)
        {
            ShowMappedError(exception);
        }
    }

    public async Task CopyPasswordAsync(Guid itemId)
    {
        var version = LifecycleVersion;
        try
        {
            var value = await flow.GetPasswordAsync(itemId, string.Empty);
            if (IsCurrentUnlock(version)) await clipboard.CopyAsync(value);
        }
        catch (Exception exception)
        {
            ShowMappedError(exception);
        }
    }

    public async Task ViewPasswordHistoryAsync(Guid itemId)
    {
        var version = LifecycleVersion;
        if (Vault.Items.All(item => item.Id != itemId || !item.HasPassword)) return;
        try
        {
            var history = await flow.GetPasswordHistoryAsync(itemId, string.Empty);
            if (!IsCurrentUnlock(version)) return;
            var text = history.Count == 0
                ? "No previous passwords are stored for this item."
                : string.Join(Environment.NewLine + Environment.NewLine, history.Select(entry =>
                    $"Changed {entry.ChangedAt.ToLocalTime():g}{Environment.NewLine}{entry.Password}"));
            await dialogs.ShowSecretAsync("Password history", text, multiline: true);
        }
        catch (Exception exception)
        {
            ShowMappedError(exception);
        }
    }

    partial void OnFlowStateChanged(AppFlowState value) => OnPropertyChanged(nameof(IsUnlocked));

    private async Task CompleteUnlockAsync(VaultUnlockResult result)
    {
        var version = LifecycleVersion;
        StatusMessage = result.Message;
        IsStatusOpen = !string.IsNullOrWhiteSpace(result.Message);
        InactivityTimeout = await flow.GetInactivityTimeoutAsync();
        if (!IsCurrentUnlock(version)) return;
        VaultOpenDuration = await flow.GetVaultOpenDurationAsync();
        await Vault.RefreshAsync();
    }

    private void ShowMappedError(Exception exception)
    {
        StatusMessage = errorMapper.Map(exception);
        IsStatusOpen = true;
    }

    private void ClearAuthenticationStatus()
    {
        StatusMessage = string.Empty;
        IsStatusOpen = false;
    }

    private static string FormatInspection(VaultBackupInspection inspection) =>
        $"{inspection.Format} v{inspection.Version} · {inspection.TotalItemCount:N0} items · " +
        $"{inspection.ActiveItemCount:N0} active · {inspection.TrashItemCount:N0} in Trash";

}
