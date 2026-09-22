using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PasswordTool.Core.Models;

namespace PasswordTool.Presentation;

public sealed partial class SecurityCheckViewModel(
    AppFlowCoordinator flow,
    IUserDialogService dialogs,
    IUserErrorMapper errorMapper) : ObservableObject
{
    public ObservableCollection<VaultSecurityFinding> Findings { get; } = [];

    [ObservableProperty] public partial VaultSecurityFinding? SelectedFinding { get; set; }
    [ObservableProperty] public partial string Summary { get; set; } = "Run a local scan to find weak, reused, and old passwords.";
    [ObservableProperty] public partial string ErrorMessage { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsErrorOpen { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }

    public async Task RunAsync()
    {
        var code = await RequestSensitiveCodeAsync();
        if (code is null) return;
        IsBusy = true;
        IsErrorOpen = false;
        try
        {
            var findings = await flow.GetSecurityFindingsAsync(code);
            Findings.Clear();
            foreach (var finding in findings) Findings.Add(finding);
            var affected = findings.Select(finding => finding.ItemId).Distinct().Count();
            Summary = findings.Count == 0
                ? "No weak, reused, or one-year-old passwords found."
                : $"{findings.Count:N0} findings across {affected:N0} affected items. Password values are never shown.";
        }
        catch (Exception exception)
        {
            ErrorMessage = errorMapper.Map(exception);
            IsErrorOpen = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<string?> RequestSensitiveCodeAsync()
    {
        if (await flow.IsSensitiveSessionActiveAsync()) return string.Empty;
        return await dialogs.PromptSensitiveTotpAsync("Run Security Check", "Confirm before scanning decrypted password values in memory.");
    }
}
