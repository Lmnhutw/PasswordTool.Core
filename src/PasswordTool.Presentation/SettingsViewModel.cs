using CommunityToolkit.Mvvm.ComponentModel;
using PasswordTool.Core.Models;

namespace PasswordTool.Presentation;

public sealed partial class SettingsViewModel(AppFlowCoordinator flow, IUserErrorMapper errorMapper) : ObservableObject
{
    public IReadOnlyList<LoginModeOption> LoginModes { get; } =
    [
        new("Master Password or Authenticator", VaultLoginMode.Hybrid),
        new("Prefer Authenticator", VaultLoginMode.GoogleAuthenticatorCode)
    ];

    [ObservableProperty] public partial LoginModeOption SelectedLoginMode { get; set; } = null!;
    [ObservableProperty] public partial double InactivityTimeoutMinutes { get; set; } = VaultSecuritySettings.DefaultInactivityLockTimeoutMinutes;
    [ObservableProperty] public partial double SensitiveActionTimeoutMinutes { get; set; } = VaultSecuritySettings.DefaultSensitiveActionTimeoutMinutes;
    [ObservableProperty] public partial bool NeedsKdfUpgrade { get; set; }
    [ObservableProperty] public partial string BackupHealthText { get; set; } = string.Empty;
    [ObservableProperty] public partial string StatusMessage { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsStatusOpen { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }

    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var snapshot = await flow.GetSettingsAsync();
            SelectedLoginMode = LoginModes.First(option => option.Value == snapshot.LoginMode);
            InactivityTimeoutMinutes = snapshot.InactivityTimeoutMinutes;
            SensitiveActionTimeoutMinutes = snapshot.SensitiveActionTimeoutMinutes;
            NeedsKdfUpgrade = snapshot.NeedsKdfUpgrade;
            BackupHealthText = $"Last external backup: {FormatDate(snapshot.LastExternalBackupAt)} · " +
                $"Last verified: {FormatDate(snapshot.LastVerifiedBackupAt)}";
        }
        catch (Exception exception)
        {
            ShowError(errorMapper.Map(exception));
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<bool> SaveAsync(string masterPassword)
    {
        if (SelectedLoginMode is null) return false;
        var result = await flow.UpdateSettingsAsync(
            masterPassword,
            SelectedLoginMode.Value,
            (int)InactivityTimeoutMinutes,
            (int)SensitiveActionTimeoutMinutes);
        return Complete(result, "Security settings saved.");
    }

    public async Task<bool> ChangeMasterPasswordAsync(string currentPassword, string newPassword, string confirmation)
    {
        if (!string.Equals(newPassword, confirmation, StringComparison.Ordinal))
        {
            ShowError("The new Master Password values do not match.");
            return false;
        }

        var result = await flow.ChangeMasterPasswordAsync(currentPassword, newPassword);
        return Complete(result, "Master Password changed.");
    }

    public async Task<bool> UpgradeKdfAsync(string masterPassword)
    {
        var result = await flow.UpgradeKdfAsync(masterPassword);
        var success = Complete(result, "The Master Password KDF is current.");
        if (success) NeedsKdfUpgrade = false;
        return success;
    }

    public AuthenticatorSetup PrepareAuthenticator() => flow.CreateAuthenticatorSetup();

    public async Task<bool> ResetAuthenticatorAsync(string masterPassword, AuthenticatorSetup setup, string code)
    {
        var result = await flow.ResetAuthenticatorAsync(masterPassword, setup, code);
        return Complete(result, "Authenticator reset. Existing trusted unlock tokens were replaced.");
    }

    private bool Complete(OperationResult result, string successMessage)
    {
        StatusMessage = result.Success ? successMessage : result.Message;
        IsStatusOpen = true;
        return result.Success;
    }

    private void ShowError(string message)
    {
        StatusMessage = message;
        IsStatusOpen = true;
    }

    private static string FormatDate(DateTimeOffset? value) => value?.ToLocalTime().ToString("g") ?? "not available";
}
