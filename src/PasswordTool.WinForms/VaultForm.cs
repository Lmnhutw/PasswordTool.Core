using System.Runtime.InteropServices;
using System.Security.Cryptography;
using PasswordTool.Core.Models;
using PasswordTool.Core.Services;

namespace PasswordTool.WinForms;

public sealed class VaultForm : Form
{
    private readonly VaultService vaultService;
    private readonly SensitiveClipboardService clipboardService = new();
    private readonly DataGridView itemsGrid = new();
    private readonly TextBox searchTextBox = new();
    private readonly CheckBox favoritesOnlyCheckBox = new() { Text = "Favorites only", AutoSize = true };
    private readonly Label sensitiveSessionLabel = new() { AutoSize = true };
    private readonly Button editButton = new() { Text = "Edit", Width = 78 };
    private readonly Button deleteButton = new() { Text = "Delete", Width = 78 };
    private readonly Button viewSecretButton = new() { Text = "View", Width = 90 };
    private readonly Button copyUsernameButton = new() { Text = "Copy User", Width = 92 };
    private readonly Button copyPasswordButton = new() { Text = "Copy Password", Width = 112 };
    private readonly Button copyTotpButton = new() { Text = "Copy TOTP", Width = 96 };
    private readonly System.Windows.Forms.Timer sessionTimer = new() { Interval = 1_000 };
    private IReadOnlyList<VaultItem> allItems = [];

    public bool LockRequested { get; private set; }

    public VaultForm(VaultService vaultService)
    {
        this.vaultService = vaultService;
        BuildInterface();
        LoadItems();
        FormIconService.Apply(this);
        sessionTimer.Tick += (_, _) => UpdateSensitiveSessionLabel();
        sessionTimer.Start();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        sessionTimer.Dispose();
        clipboardService.Dispose();
        vaultService.ClearSession();
        base.OnFormClosed(e);
    }

    private void BuildInterface()
    {
        Text = "Password Vault";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(1280, 720);
        MinimumSize = new Size(1050, 620);
        Padding = new Padding(12);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true };
        var addButton = CreateToolbarButton("Add", 72, (_, _) => AddItem());
        var settingsButton = CreateToolbarButton("Settings", 88, (_, _) => OpenSettings());
        var importCsvButton = CreateToolbarButton("Import CSV", 96, (_, _) => ImportCsv());
        var importBackupButton = CreateToolbarButton("Import Backup", 112, (_, _) => ImportBackup());
        var exportBackupButton = CreateToolbarButton("Export Backup", 112, (_, _) => ExportBackup());
        var hashToolButton = CreateToolbarButton("Hash Tool", 92, (_, _) => OpenHashTool());
        var lockButton = CreateToolbarButton("Lock", 72, (_, _) => LockVault());
        editButton.Click += (_, _) => EditSelectedItem();
        deleteButton.Click += (_, _) => DeleteSelectedItem();
        viewSecretButton.Click += (_, _) => ViewSelectedSecret();
        copyUsernameButton.Click += (_, _) => CopySelectedUsername();
        copyPasswordButton.Click += (_, _) => CopySelectedPassword();
        copyTotpButton.Click += (_, _) => CopySelectedTotp();
        toolbar.Controls.AddRange([addButton, editButton, deleteButton, viewSecretButton, copyUsernameButton,
            copyPasswordButton, copyTotpButton, importCsvButton, importBackupButton, exportBackupButton,
            settingsButton, hashToolButton, lockButton]);

        var filterRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4 };
        filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
        filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        filterRow.Controls.Add(new Label { Text = "Search", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        searchTextBox.Dock = DockStyle.Fill;
        searchTextBox.PlaceholderText = "Title, username, URL, folder, or tag";
        searchTextBox.TextChanged += (_, _) => ApplyFilters();
        favoritesOnlyCheckBox.CheckedChanged += (_, _) => ApplyFilters();
        filterRow.Controls.Add(searchTextBox, 1, 0);
        filterRow.Controls.Add(favoritesOnlyCheckBox, 2, 0);
        filterRow.Controls.Add(sensitiveSessionLabel, 3, 0);

        ConfigureGrid();
        root.Controls.Add(toolbar, 0, 0);
        root.Controls.Add(filterRow, 0, 1);
        root.Controls.Add(itemsGrid, 0, 2);
        Controls.Add(root);
        UpdateSensitiveSessionLabel();
    }

    private void ConfigureGrid()
    {
        itemsGrid.Dock = DockStyle.Fill;
        itemsGrid.AllowUserToAddRows = false;
        itemsGrid.AllowUserToDeleteRows = false;
        itemsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        itemsGrid.BackgroundColor = SystemColors.Window;
        itemsGrid.ClipboardCopyMode = DataGridViewClipboardCopyMode.Disable;
        itemsGrid.MultiSelect = false;
        itemsGrid.ReadOnly = true;
        itemsGrid.RowHeadersVisible = false;
        itemsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        itemsGrid.SelectionChanged += (_, _) => UpdateButtonState();
        itemsGrid.CellDoubleClick += (_, _) => EditSelectedItem();
        itemsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Favorite", HeaderText = "★", FillWeight = 5 });
        itemsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Title", HeaderText = "Title", FillWeight = 20 });
        itemsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Type", HeaderText = "Type", FillWeight = 12 });
        itemsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Username", HeaderText = "Username", FillWeight = 18 });
        itemsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Folder", HeaderText = "Folder", FillWeight = 12 });
        itemsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Tags", HeaderText = "Tags", FillWeight = 16 });
        itemsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Totp", HeaderText = "TOTP", FillWeight = 8 });
        itemsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Url", HeaderText = "URL", FillWeight = 20 });
        itemsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "UpdatedAt", HeaderText = "Updated", FillWeight = 12 });
    }

    private void LoadItems()
    {
        allItems = vaultService.GetItems();
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var search = searchTextBox.Text.Trim();
        var filtered = allItems.Where(item => !favoritesOnlyCheckBox.Checked || item.IsFavorite)
            .Where(item => search.Length == 0 || MatchesSearch(item, search));
        itemsGrid.Rows.Clear();
        foreach (var item in filtered)
        {
            var rowIndex = itemsGrid.Rows.Add(
                item.IsFavorite ? "★" : string.Empty,
                item.Title,
                item.Type == VaultItemType.Password ? "Password" : "Recovery codes",
                item.Username,
                item.Folder,
                string.Join(", ", item.Tags),
                item.HasTotp ? "Yes" : string.Empty,
                item.HideUrl ? "Hidden" : item.Url,
                item.UpdatedAt.ToLocalTime().ToString("g"));
            itemsGrid.Rows[rowIndex].Tag = new VaultItemRowTag(item.Id, item.Type, item.HasTotp);
        }
        UpdateButtonState();
    }

    private static bool MatchesSearch(VaultItem item, string search) =>
        Contains(item.Title, search) || Contains(item.Username, search) || Contains(item.Url, search)
        || Contains(item.Folder, search) || item.Tags.Any(tag => Contains(tag, search));

    private static bool Contains(string? value, string search) => value?.Contains(search, StringComparison.CurrentCultureIgnoreCase) == true;

    private void AddItem()
    {
        using var editor = new VaultItemEditorForm();
        if (editor.ShowDialog(this) != DialogResult.OK || editor.DeleteRequested) return;
        RunVaultAction(() => { vaultService.AddItem(editor.Item); LoadItems(); });
    }

    private void EditSelectedItem()
    {
        if (!TryGetSelectedItem(out var selected)) return;
        var code = RequestSensitiveAuthorization("Edit Vault Item", "Confirm with the PasswordTool Authenticator code before editing this item.");
        if (code is null) return;
        RunVaultAction(() =>
        {
            var item = vaultService.GetItemForEditing(selected.Id, code);
            using var editor = new VaultItemEditorForm(item);
            if (editor.ShowDialog(this) != DialogResult.OK) return;
            if (editor.DeleteRequested) vaultService.DeleteItem(selected.Id);
            else vaultService.UpdateItem(editor.Item);
            LoadItems();
        });
    }

    private void DeleteSelectedItem()
    {
        if (!TryGetSelectedItem(out var selected)) return;
        if (MessageBox.Show("Delete the selected vault item?", "PasswordTool", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        RunVaultAction(() => { vaultService.DeleteItem(selected.Id); LoadItems(); });
    }

    private void ViewSelectedSecret()
    {
        if (!TryGetSelectedItem(out var selected)) return;
        var code = RequestSensitiveAuthorization("View Secret", "Confirm with the PasswordTool Authenticator code before viewing this secret.");
        if (code is null) return;
        RunVaultAction(() =>
        {
            if (selected.Type == VaultItemType.Password) ShowTextDialog("Password", vaultService.GetPassword(selected.Id, code));
            else ShowTextDialog("Recovery Codes", string.Join(Environment.NewLine, vaultService.GetRecoveryCodes(selected.Id, code)), multiline: true);
        });
    }

    private void CopySelectedUsername()
    {
        if (!TryGetSelectedItem(out var selected)) return;
        RunVaultAction(() => CopyWithNotice(vaultService.GetUsername(selected.Id), "Username"));
    }

    private void CopySelectedPassword()
    {
        if (!TryGetSelectedItem(out var selected) || selected.Type != VaultItemType.Password) return;
        var code = RequestSensitiveAuthorization("Copy Password", "Confirm with the PasswordTool Authenticator code before copying this password.");
        if (code is null) return;
        RunVaultAction(() => CopyWithNotice(vaultService.GetPassword(selected.Id, code), "Password"));
    }

    private void CopySelectedTotp()
    {
        if (!TryGetSelectedItem(out var selected) || !selected.HasTotp) return;
        var code = RequestSensitiveAuthorization("Copy Website TOTP", "Confirm with the PasswordTool Authenticator code before copying this website code.");
        if (code is null) return;
        RunVaultAction(() =>
        {
            var result = vaultService.GetWebsiteTotpCode(selected.Id, code);
            CopyWithNotice(result.Code, $"Website TOTP code ({result.SecondsRemaining}s remaining)");
        });
    }

    private void CopyWithNotice(string value, string label)
    {
        clipboardService.Copy(value);
        MessageBox.Show($"{label} copied. PasswordTool will clear it after 30 seconds if the clipboard is unchanged.", "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ImportCsv()
    {
        if (MessageBox.Show("CSV exports contain plaintext passwords. Import only a file you trust, then securely remove it when finished.", "Import CSV", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;
        using var dialog = new OpenFileDialog { Title = "Import Password CSV", Filter = "CSV files (*.csv)|*.csv", CheckFileExists = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        string csv;
        try
        {
            var info = new FileInfo(dialog.FileName);
            if (info.Length > VaultCsvImportService.MaxCsvCharacters) throw new InvalidDataException("The selected CSV exceeds the 10 MB limit.");
            csv = File.ReadAllText(dialog.FileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            ShowError(ex.Message);
            return;
        }
        var code = RequestSensitiveAuthorization("Import CSV", "Confirm with the PasswordTool Authenticator code before reviewing plaintext imported passwords.");
        if (code is null) return;
        RunVaultAction(() =>
        {
            var plan = vaultService.PreviewCsvImport(csv, code);
            using var review = new VaultCsvImportReviewForm(plan);
            if (review.ShowDialog(this) != DialogResult.OK) return;
            var count = vaultService.ImportCsv(csv, string.Empty);
            LoadItems();
            MessageBox.Show($"Imported {count} new account(s). Existing matching accounts were not overwritten.", "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
    }

    private void ExportBackup()
    {
        using var passphraseForm = new BackupPassphraseForm(requireConfirmation: true);
        if (passphraseForm.ShowDialog(this) != DialogResult.OK) return;
        using var dialog = new SaveFileDialog
        {
            Title = "Export Encrypted PasswordTool Backup",
            Filter = "PasswordTool JSON backup (*.json)|*.json",
            AddExtension = true,
            DefaultExt = "json",
            FileName = $"PasswordTool-backup-{DateTime.Now:yyyy-MM-dd}.json",
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var code = RequestSensitiveAuthorization("Export Backup", "Confirm with the PasswordTool Authenticator code before exporting vault secrets.");
        if (code is null) return;
        RunVaultAction(() =>
        {
            File.WriteAllText(dialog.FileName, vaultService.ExportBackupJson(passphraseForm.Passphrase, code));
            MessageBox.Show("The encrypted backup was exported successfully.", "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
    }

    private void ImportBackup()
    {
        using var dialog = new OpenFileDialog { Title = "Import Encrypted PasswordTool Backup", Filter = "PasswordTool JSON backup (*.json)|*.json", CheckFileExists = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        string json;
        try
        {
            var info = new FileInfo(dialog.FileName);
            if (info.Length > VaultBackupService.MaxBackupJsonCharacters) throw new InvalidDataException("The selected backup exceeds the 10 MB limit.");
            json = File.ReadAllText(dialog.FileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            ShowError(ex.Message);
            return;
        }
        using var passphraseForm = new BackupPassphraseForm(requireConfirmation: false);
        if (passphraseForm.ShowDialog(this) != DialogResult.OK) return;
        var code = RequestSensitiveAuthorization("Import Backup", "Confirm with the PasswordTool Authenticator code before reviewing imported items.");
        if (code is null) return;
        RunVaultAction(() =>
        {
            var plan = vaultService.PreviewBackupImport(json, passphraseForm.Passphrase, code);
            using var review = new VaultImportReviewForm(plan);
            if (review.ShowDialog(this) != DialogResult.OK) return;
            var count = vaultService.ImportBackupJson(json, passphraseForm.Passphrase, string.Empty);
            LoadItems();
            MessageBox.Show($"Imported {count} new item(s). Existing items were not overwritten.", "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
    }

    private string? RequestSensitiveAuthorization(string title, string prompt)
    {
        if (!vaultService.IsGoogleAuthenticatorConfigured || vaultService.IsSensitiveSessionActive) return string.Empty;
        using var form = new VerifyTotpForm(title, prompt, vaultService.VerifyTotpForSensitiveAction);
        return form.ShowDialog(this) == DialogResult.OK ? form.Code : null;
    }

    private void UpdateSensitiveSessionLabel()
    {
        if (vaultService.SensitiveSessionExpiresAt is not { } expiresAt)
        {
            sensitiveSessionLabel.Text = "Sensitive actions: locked";
            return;
        }
        var remaining = expiresAt - DateTimeOffset.UtcNow;
        sensitiveSessionLabel.Text = remaining > TimeSpan.Zero
            ? $"Sensitive actions unlocked: {remaining.Minutes}:{remaining.Seconds:D2}"
            : "Sensitive actions: locked";
    }

    private void OpenSettings()
    {
        using var settings = new VaultSettingsForm(vaultService);
        settings.ShowDialog(this);
    }

    private void OpenHashTool()
    {
        using var form = new PasswordHashToolForm();
        form.ShowDialog(this);
    }

    private void LockVault()
    {
        LockRequested = true;
        Close();
    }

    private void ShowTextDialog(string title, string value, bool multiline = false)
    {
        using var dialog = new Form
        {
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ClientSize = multiline ? new Size(580, 420) : new Size(540, 150),
            Padding = new Padding(16)
        };
        FormIconService.Apply(dialog);
        var textBox = new TextBox { Text = value, Dock = DockStyle.Fill, ReadOnly = true, Multiline = multiline, ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None, ShortcutsEnabled = false };
        var close = new Button { Text = "Close", Dock = DockStyle.Bottom, Height = 34, DialogResult = DialogResult.OK };
        dialog.Controls.Add(textBox);
        dialog.Controls.Add(close);
        dialog.AcceptButton = close;
        dialog.ShowDialog(this);
    }

    private bool TryGetSelectedItem(out VaultItemRowTag item)
    {
        item = null!;
        if (itemsGrid.CurrentRow?.Tag is not VaultItemRowTag selected) return false;
        item = selected;
        return true;
    }

    private void UpdateButtonState()
    {
        var selected = TryGetSelectedItem(out var item);
        editButton.Enabled = selected;
        deleteButton.Enabled = selected;
        viewSecretButton.Enabled = selected;
        copyUsernameButton.Enabled = selected;
        copyPasswordButton.Enabled = selected && item.Type == VaultItemType.Password;
        copyTotpButton.Enabled = selected && item.HasTotp;
        viewSecretButton.Text = selected && item.Type == VaultItemType.RecoveryCodes ? "View Codes" : "View Password";
    }

    private static Button CreateToolbarButton(string text, int width, EventHandler handler)
    {
        var button = new Button { Text = text, Width = width, Height = 32 };
        button.Click += handler;
        return button;
    }

    private static void RunVaultAction(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or UnauthorizedAccessException
            or IOException or InvalidDataException or CryptographicException or ExternalException)
        {
            ShowError(ex.Message);
        }
    }

    private static void ShowError(string message) => MessageBox.Show(message, "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Error);

    private sealed record VaultItemRowTag(Guid Id, VaultItemType Type, bool HasTotp);
}
