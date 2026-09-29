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
    [ObservableProperty] public partial TimeSpan InactivityTimeout { get; set; } = TimeSpan.FromMinutes(10);
    [ObservableProperty] public partial bool CanUseAuthenticator { get; set; }
    [ObservableProperty] public partial bool UseAuthenticator { get; set; }
    [ObservableProperty] public partial bool IsRecovering { get; set; }
    [ObservableProperty] public partial string RecoveryPath { get; set; } = string.Empty;
    [ObservableProperty] public partial string RecoverySummary { get; set; } = string.Empty;
    [ObservableProperty] public partial AuthenticatorSetup? PendingAuthenticatorSetup { get; set; }

    public bool IsUnlocked => FlowState == AppFlowState.Unlocked;
    public bool HasPartialStorage => flow.HasPartialStorage;
    public bool IsVaultRoute => CurrentRoute == AppRoute.Vault;
    public bool IsHashToolRoute => CurrentRoute == AppRoute.HashTool;

    public async Task UnlockAsync(string masterPassword)
    {
        IsStatusOpen = false;
        var result = await flow.UnlockAsync(masterPassword);
        FlowState = flow.FlowState;
        if (!result.Success)
        {
            StatusMessage = result.Message;
            IsStatusOpen = true;
            return;
        }

        await CompleteUnlockAsync(result);
    }

    public async Task LoadAuthenticationOptionsAsync()
    {
        if (FlowState != AppFlowState.Unlock) return;
        try
        {
            var options = await flow.GetUnlockOptionsAsync();
            CanUseAuthenticator = options.CanUseAuthenticator;
            UseAuthenticator = options.CanUseAuthenticator
                && options.PreferredLoginMode == VaultLoginMode.GoogleAuthenticatorCode;
        }
        catch (Exception exception)
        {
            ShowMappedError(exception);
        }
    }

    public async Task UnlockWithAuthenticatorAsync(string code)
    {
        IsStatusOpen = false;
        var result = await flow.UnlockWithAuthenticatorAsync(code);
        FlowState = flow.FlowState;
        if (!result.Success)
        {
            StatusMessage = result.Message;
            IsStatusOpen = true;
            return;
        }

        await CompleteUnlockAsync(result);
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
        await flow.LockAsync();
        Vault.Clear();
        await clipboard.ClearOwnedValueAsync();
        navigation.ResetForLock();
        FlowState = flow.FlowState;
        CurrentRoute = navigation.CurrentRoute;
        StatusMessage = string.Empty;
        IsStatusOpen = false;
        PendingAuthenticatorSetup = null;
    }

    public void Navigate(AppRoute route)
    {
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

    public async Task<VaultItem?> GetItemForEditingAsync(Guid itemId, string title = "Edit item", string message = "Confirm before loading secret fields for editing.")
    {
        var code = await RequestSensitiveCodeAsync(title, message);
        if (code is null) return null;
        try
        {
            return await flow.GetItemForEditingAsync(itemId, code);
        }
        catch (Exception exception)
        {
            ShowMappedError(exception);
            return null;
        }
    }

    public async Task<bool> SaveItemAsync(VaultItemEditorInput input)
    {
        try
        {
            var item = input.ToVaultItem();
            if (input.Id is null) await flow.AddItemAsync(item);
            else await flow.UpdateItemAsync(item);
            await Vault.RefreshAsync();
            Navigate(AppRoute.Vault);
            ClearAuthenticationStatus();
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
        if (!await dialogs.ConfirmAsync("Delete group", $"Delete '{name}'? Its credentials will move to Ungrouped.", "Delete group")) return;
        try { await flow.DeleteGroupAsync(id); await Vault.RefreshAsync(); }
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
        try
        {
            await clipboard.CopyAsync(await flow.GetUsernameAsync(itemId));
        }
        catch (Exception exception)
        {
            ShowMappedError(exception);
        }
    }

    public async Task RevealPasswordAsync(Guid itemId)
    {
        var code = await RequestSensitiveCodeAsync("View password", "Confirm before revealing this password.");
        if (code is null) return;
        try
        {
            await dialogs.ShowSecretAsync("Password", await flow.GetPasswordAsync(itemId, code), multiline: false);
        }
        catch (Exception exception)
        {
            ShowMappedError(exception);
        }
    }

    public async Task RevealRecoveryCodesAsync(Guid itemId)
    {
        var code = await RequestSensitiveCodeAsync("View recovery codes", "Confirm before revealing recovery codes.");
        if (code is null) return;
        try
        {
            var codes = await flow.GetRecoveryCodesAsync(itemId, code);
            await dialogs.ShowSecretAsync("Recovery codes", string.Join(Environment.NewLine, codes), multiline: true);
        }
        catch (Exception exception)
        {
            ShowMappedError(exception);
        }
    }

    public async Task RevealNotesAsync(Guid itemId)
    {
        var item = Vault.Items.FirstOrDefault(candidate => candidate.Id == itemId);
        if (item is null || !item.HasNotes) return;

        var notes = item.Notes;
        if (item.HideNotes)
        {
            var fullItem = await GetItemForEditingAsync(itemId, "View notes", "Confirm before revealing hidden notes.");
            if (fullItem is null) return;
            notes = fullItem.Notes;
        }

        try
        {
            await dialogs.ShowSecretAsync("Notes", notes, multiline: true);
        }
        catch (Exception exception)
        {
            ShowMappedError(exception);
        }
    }

    public async Task CopyPasswordAsync(Guid itemId)
    {
        var code = await RequestSensitiveCodeAsync("Copy password", "Confirm before copying this password.");
        if (code is null) return;
        try
        {
            await clipboard.CopyAsync(await flow.GetPasswordAsync(itemId, code));
        }
        catch (Exception exception)
        {
            ShowMappedError(exception);
        }
    }

    public async Task ViewPasswordHistoryAsync(Guid itemId)
    {
        if (Vault.Items.All(item => item.Id != itemId || !item.HasPassword)) return;
        var code = await RequestSensitiveCodeAsync("Password history", "Confirm before revealing previous passwords.");
        if (code is null) return;
        try
        {
            var history = await flow.GetPasswordHistoryAsync(itemId, code);
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
        StatusMessage = result.Message;
        IsStatusOpen = !string.IsNullOrWhiteSpace(result.Message);
        InactivityTimeout = await flow.GetInactivityTimeoutAsync();
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

    private async Task<string?> RequestSensitiveCodeAsync(string title, string message)
    {
        if (await flow.IsSensitiveSessionActiveAsync()) return string.Empty;
        return await dialogs.PromptSensitiveTotpAsync(title, message);
    }
}
