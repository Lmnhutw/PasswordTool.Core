using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PasswordTool.Core.Models;

namespace PasswordTool.Presentation;

public sealed partial class BackupViewModel(
    AppFlowCoordinator flow,
    IFilePickerService filePicker,
    IUserDialogService dialogs,
    IUserErrorMapper errorMapper,
    VaultWorkspaceViewModel vault) : ObservableObject
{
    [ObservableProperty] public partial string SelectedBackupPath { get; set; } = string.Empty;
    [ObservableProperty] public partial string SelectedCsvPath { get; set; } = string.Empty;
    [ObservableProperty] public partial string Summary { get; set; } = string.Empty;
    [ObservableProperty] public partial string StatusMessage { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsStatusOpen { get; set; }
    [ObservableProperty] public partial bool CanImportBackup { get; set; }
    [ObservableProperty] public partial bool CanImportCsv { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial VaultSnapshotInfo? SelectedSnapshot { get; set; }

    public ObservableCollection<VaultSnapshotInfo> Snapshots { get; } = [];

    public async Task LoadAsync()
    {
        var snapshots = await flow.GetSnapshotsAsync();
        Snapshots.Clear();
        foreach (var snapshot in snapshots) Snapshots.Add(snapshot);
    }

    public async Task ExportAsync(string passphrase)
    {
        var path = await filePicker.PickSavePathAsync($"PasswordTool-backup-{DateTime.Now:yyyyMMdd-HHmm}.json");
        if (path is null) return;
        var code = await RequestSensitiveCodeAsync("Export backup", "Confirm before exporting encrypted vault items.");
        if (code is null) return;
        await RunAsync(async () =>
        {
            await flow.CreateExternalBackupAsync(path, passphrase, code);
            ShowStatus("Encrypted backup created and recorded in backup health.");
        });
    }

    public async Task VerifyAsync(string passphrase)
    {
        var path = await filePicker.PickOpenPathAsync();
        if (path is null) return;
        await RunAsync(async () =>
        {
            var inspection = await flow.VerifyExternalBackupAsync(path, passphrase);
            SelectedBackupPath = path;
            Summary = FormatInspection(inspection);
            ShowStatus("Backup authenticated successfully.");
        });
    }

    public async Task PreviewImportAsync(string passphrase)
    {
        var path = await filePicker.PickOpenPathAsync();
        if (path is null) return;
        var code = await RequestSensitiveCodeAsync("Import backup", "Confirm before inspecting an encrypted backup for import.");
        if (code is null) return;
        await RunAsync(async () =>
        {
            var plan = await flow.PreviewBackupImportAsync(path, passphrase, code);
            SelectedBackupPath = path;
            Summary = $"{plan.NewItemCount:N0} new · {plan.DuplicateCount:N0} duplicate · {plan.ConflictCount:N0} conflict";
            CanImportBackup = plan.NewItemCount > 0;
            ShowStatus("Backup import preview is ready. Only new IDs will be added.");
        });
    }

    public async Task ImportBackupAsync(string passphrase)
    {
        if (!CanImportBackup) return;
        var code = await RequestSensitiveCodeAsync("Import backup", "Confirm before adding the previewed backup items.");
        if (code is null) return;
        await RunAsync(async () =>
        {
            var count = await flow.ImportBackupAsync(SelectedBackupPath, passphrase, code);
            await vault.RefreshAsync();
            CanImportBackup = false;
            ShowStatus($"Imported {count:N0} new item{(count == 1 ? string.Empty : "s")}.");
        });
    }

    public async Task PreviewCsvAsync()
    {
        var path = await filePicker.PickOpenPathAsync();
        if (path is null) return;
        var code = await RequestSensitiveCodeAsync("Import CSV", "Confirm before inspecting plaintext credential data.");
        if (code is null) return;
        await RunAsync(async () =>
        {
            var plan = await flow.PreviewCsvImportAsync(path, code);
            SelectedCsvPath = path;
            Summary = $"{plan.NewItemCount:N0} new · {plan.DuplicateCount:N0} duplicate";
            CanImportCsv = plan.NewItemCount > 0;
            ShowStatus("CSV preview is ready. Plaintext source files are never copied into PasswordTool storage.");
        });
    }

    public async Task ImportCsvAsync()
    {
        if (!CanImportCsv) return;
        var code = await RequestSensitiveCodeAsync("Import CSV", "Confirm before adding the previewed CSV items.");
        if (code is null) return;
        await RunAsync(async () =>
        {
            var count = await flow.ImportCsvAsync(SelectedCsvPath, code);
            await vault.RefreshAsync();
            CanImportCsv = false;
            ShowStatus($"Imported {count:N0} new item{(count == 1 ? string.Empty : "s")}.");
        });
    }

    public async Task<bool> RestoreSnapshotAsync(string masterPassword)
    {
        if (SelectedSnapshot is null) return false;
        if (!await dialogs.ConfirmAsync("Restore snapshot", "Restore the selected config/vault pair and return to Unlock?", "Restore")) return false;
        var result = await flow.RestoreSnapshotAsync(SelectedSnapshot.Id, masterPassword);
        ShowStatus(result.Success ? "Snapshot restored. Unlock the restored vault to continue." : result.Message);
        return result.Success;
    }

    private async Task<string?> RequestSensitiveCodeAsync(string title, string message)
    {
        if (await flow.IsSensitiveSessionActiveAsync()) return string.Empty;
        return await dialogs.PromptSensitiveTotpAsync(title, message);
    }

    private async Task RunAsync(Func<Task> action)
    {
        IsBusy = true;
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            ShowStatus(errorMapper.Map(exception));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ShowStatus(string message)
    {
        StatusMessage = message;
        IsStatusOpen = true;
    }

    private static string FormatInspection(VaultBackupInspection inspection) =>
        $"{inspection.Format} v{inspection.Version} · {inspection.TotalItemCount:N0} items · " +
        $"{inspection.ActiveItemCount:N0} active · {inspection.TrashItemCount:N0} in Trash";
}
