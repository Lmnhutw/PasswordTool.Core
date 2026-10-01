using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PasswordTool.Core.Models;
using PasswordTool.Core.Services;

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
    [ObservableProperty] public partial string BackupPassword { get; set; } = string.Empty;
    [ObservableProperty] public partial string ConfirmBackupPassword { get; set; } = string.Empty;
    [ObservableProperty] public partial VaultSnapshotInfo? SelectedSnapshot { get; set; }

    private long actionVersion;

    public ObservableCollection<VaultSnapshotInfo> Snapshots { get; } = [];

    public async Task LoadAsync()
    {
        var version = flow.LifecycleVersion;
        IReadOnlyList<VaultSnapshotInfo> snapshots;
        try { snapshots = await flow.GetSnapshotsAsync(); }
        catch (OperationCanceledException) { return; }
        if (!flow.IsCurrentUnlock(version)) return;
        Snapshots.Clear();
        foreach (var snapshot in snapshots) Snapshots.Add(snapshot);
    }

    public async Task ExportAsync()
    {
        if (IsBusy) return;
        try { VaultBackupService.ValidatePassphrase(BackupPassword); }
        catch (ArgumentException)
        {
            ShowStatus("Use a backup password with at least 12 characters.");
            return;
        }
        if (!string.Equals(BackupPassword, ConfirmBackupPassword, StringComparison.Ordinal))
        {
            ShowStatus("The backup passwords do not match.");
            return;
        }
        await RunAsync(async () =>
        {
            var path = await filePicker.PickSavePathAsync($"PasswordTool-backup-{DateTime.Now:yyyyMMdd-HHmm}.json");
            EnsureCurrent();
            if (path is null) return;
            try
            {
                await flow.CreateExternalBackupAsync(path, BackupPassword, string.Empty);
                ShowStatus("Backup created and verified.");
            }
            finally
            {
                BackupPassword = string.Empty;
                ConfirmBackupPassword = string.Empty;
            }
        });
    }

    public async Task VerifyAsync()
    {
        await RunAsync(async () =>
        {
            var path = await filePicker.PickOpenPathAsync();
            EnsureCurrent();
            if (path is null) return;
            try
            {
                var inspection = await flow.VerifyExternalBackupAsync(path, BackupPassword);
            EnsureCurrent();
                SelectedBackupPath = path;
                Summary = FormatInspection(inspection);
                ShowStatus("Backup verified. Check it again later to confirm it is still readable.");
            }
            finally
            {
                BackupPassword = string.Empty;
                ConfirmBackupPassword = string.Empty;
            }
        });
    }

    public async Task PreviewImportAsync()
    {
        await RunAsync(async () =>
        {
            var path = await filePicker.PickOpenPathAsync();
            EnsureCurrent();
            if (path is null) return;
            CanImportBackup = false;
            var plan = await flow.PreviewBackupImportAsync(path, BackupPassword, string.Empty);
            EnsureCurrent();
            SelectedBackupPath = path;
            Summary = $"{plan.NewItemCount:N0} new · {plan.DuplicateCount:N0} duplicate · {plan.ConflictCount:N0} conflict";
            CanImportBackup = plan.NewItemCount > 0;
            ShowStatus("Backup import preview is ready. Only new IDs will be added.");
        });
    }

    public async Task ImportBackupAsync()
    {
        if (!CanImportBackup) return;
        await RunAsync(async () =>
        {
            var count = await flow.ImportBackupAsync(SelectedBackupPath, BackupPassword, string.Empty);
            EnsureCurrent();
            await vault.RefreshAsync();
            EnsureCurrent();
            CanImportBackup = false;
            ShowStatus($"Imported {count:N0} new item{(count == 1 ? string.Empty : "s")}.");
        });
    }

    public async Task PreviewCsvAsync()
    {
        await RunAsync(async () =>
        {
            var path = await filePicker.PickOpenPathAsync();
            EnsureCurrent();
            if (path is null) return;
            var plan = await flow.PreviewCsvImportAsync(path, string.Empty);
            EnsureCurrent();
            SelectedCsvPath = path;
            Summary = $"{plan.NewItemCount:N0} new · {plan.DuplicateCount:N0} duplicate";
            CanImportCsv = plan.NewItemCount > 0;
            ShowStatus("CSV preview is ready. Plaintext source files are never copied into PasswordTool storage.");
        });
    }

    public async Task ImportCsvAsync()
    {
        if (!CanImportCsv) return;
        await RunAsync(async () =>
        {
            var count = await flow.ImportCsvAsync(SelectedCsvPath, string.Empty);
            EnsureCurrent();
            await vault.RefreshAsync();
            EnsureCurrent();
            CanImportCsv = false;
            ShowStatus($"Imported {count:N0} new item{(count == 1 ? string.Empty : "s")}.");
        });
    }

    public async Task<bool> RestoreSnapshotAsync(string masterPassword)
    {
        if (SelectedSnapshot is not { } snapshot) return false;
        var restored = false;
        await RunAsync(async () =>
        {
            if (!await dialogs.ConfirmAsync("Restore snapshot", "Restore the selected config/vault pair and return to Unlock?", "Restore")) return;
            var result = await flow.RestoreSnapshotAsync(snapshot.Id, masterPassword);
            ShowStatus(result.Success ? "Snapshot restored. Unlock the restored vault to continue." : result.Message);
            restored = result.Success;
        });
        return restored;
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (IsBusy) return;
        IsBusy = true;
        actionVersion = flow.LifecycleVersion;
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

    partial void OnBackupPasswordChanged(string value) => CanImportBackup = false;

    public void Clear()
    {
        BackupPassword = ConfirmBackupPassword = string.Empty;
        SelectedBackupPath = SelectedCsvPath = Summary = StatusMessage = string.Empty;
        CanImportBackup = CanImportCsv = IsStatusOpen = false;
        SelectedSnapshot = null;
        Snapshots.Clear();
    }

    private void ShowStatus(string message)
    {
        StatusMessage = message;
        IsStatusOpen = true;
    }

    private void EnsureCurrent()
    {
        if (!flow.IsCurrentUnlock(actionVersion)) throw new OperationCanceledException("The vault was locked or the sign-in session expired.");
    }

    private static string FormatInspection(VaultBackupInspection inspection) =>
        $"{inspection.Format} v{inspection.Version} · {inspection.TotalItemCount:N0} items · " +
        $"{inspection.ActiveItemCount:N0} active · {inspection.TrashItemCount:N0} in Trash";
}
