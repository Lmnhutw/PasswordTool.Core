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
        var code = await RequestSensitiveCodeAsync("Edit item", "Confirm before loading secret fields for editing.");
        if (code is null) return null;
        try
        {
            return await flow.GetItemForEditingAsync(selected.Id, code);
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

    public async Task DeleteSelectedItemAsync()
    {
        if (Vault.SelectedItem is not { } selected) return;
        if (!await dialogs.ConfirmAsync("Move to Trash", $"Move '{selected.Title}' to Trash?", "Move to Trash")) return;
        try
        {
            await flow.DeleteItemAsync(selected.Id);
            await Vault.RefreshAsync();
            ClearAuthenticationStatus();
        }
        catch (Exception exception)
        {
            ShowMappedError(exception);
        }
    }

    public async Task CopySelectedUsernameAsync()
    {
        if (Vault.SelectedItem is not { } selected) return;
        try
        {
            await clipboard.CopyAsync(await flow.GetUsernameAsync(selected.Id));
        }
        catch (Exception exception)
        {
            ShowMappedError(exception);
        }
    }

    public async Task RevealSelectedSecretAsync()
    {
        if (Vault.SelectedItem is not { } selected) return;
        var code = await RequestSensitiveCodeAsync("View secret", "Confirm before revealing this vault item's secret.");
        if (code is null) return;
        try
        {
            if (selected.Type == VaultItemType.Password)
                await dialogs.ShowSecretAsync("Password", await flow.GetPasswordAsync(selected.Id, code), multiline: false);
            else
                await dialogs.ShowSecretAsync("Recovery codes", string.Join(Environment.NewLine, await flow.GetRecoveryCodesAsync(selected.Id, code)), multiline: true);
        }
        catch (Exception exception)
        {
            ShowMappedError(exception);
        }
    }

    public async Task CopySelectedPasswordAsync()
    {
        if (Vault.SelectedItem is not { Type: VaultItemType.Password } selected) return;
        var code = await RequestSensitiveCodeAsync("Copy password", "Confirm before copying this password.");
        if (code is null) return;
        try
        {
            await clipboard.CopyAsync(await flow.GetPasswordAsync(selected.Id, code));
        }
        catch (Exception exception)
        {
            ShowMappedError(exception);
        }
    }

    public async Task CopySelectedTotpAsync()
    {
        if (Vault.SelectedItem is not { HasTotp: true } selected) return;
        var code = await RequestSensitiveCodeAsync("Copy TOTP", "Confirm before copying the current website code.");
        if (code is null) return;
        try
        {
            var result = await flow.GetWebsiteTotpCodeAsync(selected.Id, code);
            await clipboard.CopyAsync(result.Code);
        }
        catch (Exception exception)
        {
            ShowMappedError(exception);
        }
    }

    public async Task ViewSelectedPasswordHistoryAsync()
    {
        if (Vault.SelectedItem is not { Type: VaultItemType.Password } selected) return;
        var code = await RequestSensitiveCodeAsync("Password history", "Confirm before revealing previous passwords.");
        if (code is null) return;
        try
        {
            var history = await flow.GetPasswordHistoryAsync(selected.Id, code);
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
