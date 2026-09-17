using System.Runtime.InteropServices;
using System.Security.Cryptography;
using PasswordTool.Core.Models;
using PasswordTool.Core.Services;

namespace PasswordTool.WinForms;

public sealed class BackupRecoveryCenterForm : Form
{
    private readonly VaultService vaultService;
    private readonly Action vaultChanged;
    private readonly Label lastBackupValue = new();
    private readonly Label lastVerifiedValue = new();
    private readonly Label warningLabel = new();

    public BackupRecoveryCenterForm(VaultService vaultService, Action vaultChanged)
    {
        this.vaultService = vaultService;
        this.vaultChanged = vaultChanged;
        BuildInterface();
        RefreshHealth();
        FormIconService.Apply(this);
    }

    public bool RequiresVaultLock { get; private set; }

    private void BuildInterface()
    {
        Text = "Backup & Recovery Center";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(650, 405);
        Padding = new Padding(18);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 8 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 205));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        lastBackupValue.Dock = DockStyle.Fill;
        lastBackupValue.TextAlign = ContentAlignment.MiddleLeft;
        lastVerifiedValue.Dock = DockStyle.Fill;
        lastVerifiedValue.TextAlign = ContentAlignment.MiddleLeft;
        warningLabel.Dock = DockStyle.Fill;
        warningLabel.ForeColor = Color.DarkRed;
        warningLabel.TextAlign = ContentAlignment.MiddleLeft;
        var snapshotNotice = new Label
        {
            Text = "Internal snapshots stay on this computer's disk. They can help undo local changes, but they are not protection against disk loss and are not a disaster-recovery backup.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        var createButton = CreateButton("Create encrypted backup", (_, _) => CreateBackup());
        var verifyButton = CreateButton("Verify existing backup", (_, _) => VerifyBackup());
        var previewButton = CreateButton("Preview restore/import", (_, _) => PreviewImport());
        var snapshotsButton = CreateButton("Open snapshots", (_, _) => OpenSnapshots());
        var closeButton = new Button { Text = "Close", Width = 105, Height = 34, DialogResult = DialogResult.OK };
        var closeRow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        closeRow.Controls.Add(closeButton);

        root.Controls.Add(CreateLabel("Last external backup"), 0, 0);
        root.Controls.Add(lastBackupValue, 1, 0);
        root.Controls.Add(CreateLabel("Last verified backup"), 0, 1);
        root.Controls.Add(lastVerifiedValue, 1, 1);
        root.Controls.Add(warningLabel, 0, 2);
        root.SetColumnSpan(warningLabel, 2);
        root.Controls.Add(snapshotNotice, 0, 3);
        root.SetColumnSpan(snapshotNotice, 2);
        root.Controls.Add(createButton, 0, 4);
        root.Controls.Add(verifyButton, 1, 4);
        root.Controls.Add(previewButton, 0, 5);
        root.Controls.Add(snapshotsButton, 1, 5);
        root.Controls.Add(closeRow, 0, 7);
        root.SetColumnSpan(closeRow, 2);
        Controls.Add(root);
        AcceptButton = closeButton;
        CancelButton = closeButton;
    }

    private void CreateBackup()
    {
        using var passphraseForm = new BackupPassphraseForm(requireConfirmation: true);
        if (passphraseForm.ShowDialog(this) != DialogResult.OK) return;
        using var dialog = new SaveFileDialog
        {
            Title = "Create Encrypted PasswordTool Backup",
            Filter = "PasswordTool JSON backup (*.json)|*.json",
            DefaultExt = "json",
            AddExtension = true,
            FileName = $"PasswordTool-backup-{DateTime.Now:yyyyMMdd-HHmmss}.json"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var code = RequestSensitiveAuthorization("Create Backup", "Confirm before exporting encrypted vault secrets.");
        if (code is null) return;

        RunAction(() =>
        {
            vaultService.CreateExternalBackupFile(dialog.FileName, passphraseForm.Passphrase, code);
            RefreshHealth();
            MessageBox.Show("The encrypted external backup was written successfully.", "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
    }

    private void VerifyBackup()
    {
        using var dialog = CreateOpenBackupDialog("Verify Encrypted PasswordTool Backup");
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        using var passphraseForm = new BackupPassphraseForm(requireConfirmation: false);
        if (passphraseForm.ShowDialog(this) != DialogResult.OK) return;

        RunAction(() =>
        {
            var inspection = vaultService.VerifyExternalBackupFile(dialog.FileName, passphraseForm.Passphrase);
            RefreshHealth();
            ShowInspection("Backup verified", inspection);
        });
    }

    private void PreviewImport()
    {
        using var dialog = CreateOpenBackupDialog("Preview PasswordTool Backup Restore/Import");
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        string json;
        try
        {
            json = ReadBoundedBackupFile(dialog.FileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            MessageBox.Show(ex.Message, "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        using var passphraseForm = new BackupPassphraseForm(requireConfirmation: false);
        if (passphraseForm.ShowDialog(this) != DialogResult.OK) return;
        var code = RequestSensitiveAuthorization("Preview Backup Import", "Confirm before reviewing imported items.");
        if (code is null) return;

        RunAction(() =>
        {
            var plan = vaultService.PreviewBackupImport(json, passphraseForm.Passphrase, code);
            using var review = new VaultImportReviewForm(plan);
            if (review.ShowDialog(this) != DialogResult.OK) return;
            var count = vaultService.ImportBackupJson(json, passphraseForm.Passphrase, string.Empty);
            vaultChanged();
            MessageBox.Show($"Imported {count} new item(s). Existing items were not overwritten.", "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
    }

    private void OpenSnapshots()
    {
        using var form = new VaultSnapshotsForm(vaultService);
        form.ShowDialog(this);
        if (!form.Restored) return;
        RequiresVaultLock = true;
        Close();
    }

    private void RefreshHealth()
    {
        var lastBackup = vaultService.LastExternalBackupAt;
        lastBackupValue.Text = FormatTimestamp(lastBackup);
        lastVerifiedValue.Text = FormatTimestamp(vaultService.LastVerifiedBackupAt);
        warningLabel.Text = lastBackup switch
        {
            null => "Warning: no successful external backup has been recorded.",
            { } value when value < DateTimeOffset.UtcNow.AddDays(-30) => "Warning: the latest recorded external backup is more than 30 days old.",
            _ => string.Empty
        };
    }

    private string? RequestSensitiveAuthorization(string title, string prompt)
    {
        if (!vaultService.IsGoogleAuthenticatorConfigured || vaultService.IsSensitiveSessionActive) return string.Empty;
        using var form = new VerifyTotpForm(title, prompt, vaultService.VerifyTotpForSensitiveAction);
        return form.ShowDialog(this) == DialogResult.OK ? form.Code : null;
    }

    private static OpenFileDialog CreateOpenBackupDialog(string title) => new()
    {
        Title = title,
        Filter = "PasswordTool JSON backup (*.json)|*.json",
        CheckFileExists = true
    };

    private static string ReadBoundedBackupFile(string path)
    {
        var info = new FileInfo(path);
        if (info.Length > VaultBackupService.MaxBackupJsonCharacters)
        {
            throw new InvalidDataException("The selected backup exceeds the 10 MB limit.");
        }
        return File.ReadAllText(path);
    }

    private static void ShowInspection(string title, VaultBackupInspection inspection)
    {
        var message = $"Format: {inspection.Format} v{inspection.Version}\n" +
            $"Created: {FormatTimestamp(inspection.CreatedAt)}\n" +
            $"Total items: {inspection.TotalItemCount}\n" +
            $"Passwords: {inspection.PasswordItemCount}\n" +
            $"Recovery-code sets: {inspection.RecoveryCodeItemCount}\n" +
            $"Active: {inspection.ActiveItemCount}\n" +
            $"Trash: {inspection.TrashItemCount}";
        MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static string FormatTimestamp(DateTimeOffset? value) =>
        value.HasValue ? value.Value.ToLocalTime().ToString("g") : "Never";

    private static Button CreateButton(string text, EventHandler handler)
    {
        var button = new Button { Text = text, Width = 205, Height = 34, Anchor = AnchorStyles.Left };
        button.Click += handler;
        return button;
    }

    private static Label CreateLabel(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft
    };

    private static void RunAction(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or UnauthorizedAccessException
            or IOException or InvalidDataException or CryptographicException or ExternalException
            or System.Text.Json.JsonException)
        {
            MessageBox.Show(ex.Message, "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
