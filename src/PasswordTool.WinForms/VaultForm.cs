using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32;
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
    private readonly Button historyButton = new() { Text = "History", Width = 82 };
    private readonly System.Windows.Forms.Timer sessionTimer = new() { Interval = 1_000 };
    private IReadOnlyList<VaultItem> allItems = [];
    private TimeSpan inactivityLockTimeout;
    private bool lifecycleEventsSubscribed;

    public bool LockRequested { get; private set; }

    public VaultForm(VaultService vaultService)
    {
        this.vaultService = vaultService;
        RefreshSecurityTimeouts();
        BuildInterface();
        LoadItems();
        FormIconService.Apply(this);
        sessionTimer.Tick += SessionTimer_Tick;
        sessionTimer.Start();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (lifecycleEventsSubscribed) return;
        SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;
        SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
        lifecycleEventsSubscribed = true;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (lifecycleEventsSubscribed)
        {
            SystemEvents.SessionSwitch -= SystemEvents_SessionSwitch;
            SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
            lifecycleEventsSubscribed = false;
        }
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
        var backupCenterButton = CreateToolbarButton("Backup & Recovery", 142, (_, _) => OpenBackupRecoveryCenter());
        var hashToolButton = CreateToolbarButton("Hash Tool", 92, (_, _) => OpenHashTool());
        var trashButton = CreateToolbarButton("Trash", 72, (_, _) => OpenTrash());
        var securityButton = CreateToolbarButton("Security Check", 112, (_, _) => OpenSecurityCheck());
        var lockButton = CreateToolbarButton("Lock", 72, (_, _) => LockVault());
        editButton.Click += (_, _) => EditSelectedItem();
        deleteButton.Click += (_, _) => DeleteSelectedItem();
        viewSecretButton.Click += (_, _) => ViewSelectedSecret();
        copyUsernameButton.Click += (_, _) => CopySelectedUsername();
        copyPasswordButton.Click += (_, _) => CopySelectedPassword();
        copyTotpButton.Click += (_, _) => CopySelectedTotp();
        historyButton.Click += (_, _) => ViewPasswordHistory();
        toolbar.Controls.AddRange([addButton, editButton, deleteButton, viewSecretButton, copyUsernameButton,
            copyPasswordButton, copyTotpButton, historyButton, importCsvButton, backupCenterButton,
            securityButton, trashButton, settingsButton, hashToolButton, lockButton]);

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
        if (TryGetSelectedItem(out var selected)) EditItem(selected.Id);
    }

    private bool EditItem(Guid id)
    {
        var code = RequestSensitiveAuthorization("Edit Vault Item", "Confirm with the PasswordTool Authenticator code before editing this item.");
        if (code is null) return false;
        var completed = false;
        RunVaultAction(() =>
        {
            var item = vaultService.GetItemForEditing(id, code);
            using var editor = new VaultItemEditorForm(item);
            if (editor.ShowDialog(this) != DialogResult.OK) return;
            if (editor.DeleteRequested) vaultService.DeleteItem(id);
            else vaultService.UpdateItem(editor.Item);
            LoadItems();
            completed = true;
        });
        return completed;
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
        if (settings.RequiresVaultLock)
        {
            LockVault();
            return;
        }
        RefreshSecurityTimeouts();
    }

    private void OpenBackupRecoveryCenter()
    {
        using var center = new BackupRecoveryCenterForm(vaultService, LoadItems);
        center.ShowDialog(this);
        if (center.RequiresVaultLock) LockVault();
    }

    private void ViewPasswordHistory()
    {
        if (!TryGetSelectedItem(out var selected) || selected.Type != VaultItemType.Password) return;
        var code = RequestSensitiveAuthorization("Password History", "Confirm before viewing previous passwords.");
        if (code is null) return;
        RunVaultAction(() =>
        {
            var history = vaultService.GetPasswordHistory(selected.Id, code);
            var text = history.Count == 0
                ? "No previous passwords are stored for this item."
                : string.Join(Environment.NewLine + Environment.NewLine,
                    history.Select(entry => $"{entry.ChangedAt.ToLocalTime():g}{Environment.NewLine}{entry.Password}"));
            ShowTextDialog("Password History (latest 10)", text, multiline: true);
        });
    }

    private void OpenSecurityCheck()
    {
        var code = RequestSensitiveAuthorization("Local Security Check", "Confirm before checking encrypted passwords locally.");
        if (code is null) return;
        RunVaultAction(() =>
        {
            while (true)
            {
                using var form = new VaultSecurityCheckForm(vaultService.GetSecurityFindings(code));
                if (form.ShowDialog(this) != DialogResult.OK || form.SelectedItemId is not { } itemId) return;
                if (!EditItem(itemId)) return;
                code = RequestSensitiveAuthorization("Local Security Check", "Confirm before refreshing the local Security Check.");
                if (code is null) return;
            }
        });
    }

    private void OpenTrash()
    {
        using var dialog = new Form
        {
            Text = "Trash - automatically removed after 30 days",
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(700, 420),
            Padding = new Padding(16)
        };
        FormIconService.Apply(dialog);
        var list = new ListBox { Dock = DockStyle.Fill };
        var restore = new Button { Text = "Restore", Width = 100 };
        var permanentDelete = new Button { Text = "Delete permanently", Width = 150 };
        var close = new Button { Text = "Close", Width = 100, DialogResult = DialogResult.Cancel };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.Add(close);
        buttons.Controls.Add(permanentDelete);
        buttons.Controls.Add(restore);
        dialog.Controls.Add(list);
        dialog.Controls.Add(buttons);

        void Reload()
        {
            list.Items.Clear();
            foreach (var item in vaultService.GetDeletedItems()) list.Items.Add(new TrashRow(item.Id, item.Title, item.DeletedAt));
        }
        restore.Click += (_, _) =>
        {
            if (list.SelectedItem is not TrashRow row) return;
            RunVaultAction(() => { vaultService.RestoreDeletedItem(row.Id); Reload(); LoadItems(); });
        };
        permanentDelete.Click += (_, _) =>
        {
            if (list.SelectedItem is not TrashRow row) return;
            if (MessageBox.Show("This cannot be undone. Delete permanently?", "PasswordTool", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            var code = RequestSensitiveAuthorization("Delete Permanently", "Confirm before permanently deleting this item.");
            if (code is null) return;
            RunVaultAction(() => { vaultService.PermanentlyDeleteItem(row.Id, code); Reload(); });
        };
        Reload();
        dialog.ShowDialog(this);
    }

    private void SessionTimer_Tick(object? sender, EventArgs e)
    {
        UpdateSensitiveSessionLabel();
        if (GetSystemIdleTime() >= inactivityLockTimeout) LockVault();
    }

    private void RefreshSecurityTimeouts()
    {
        inactivityLockTimeout = TimeSpan.FromMinutes(vaultService.SecuritySettings.InactivityLockTimeoutMinutes);
    }

    private void SystemEvents_SessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason is SessionSwitchReason.SessionLock
            or SessionSwitchReason.ConsoleDisconnect
            or SessionSwitchReason.RemoteDisconnect)
        {
            RequestLifecycleLock();
        }
    }

    private void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode is PowerModes.Suspend or PowerModes.Resume)
        {
            RequestLifecycleLock();
        }
    }

    private void RequestLifecycleLock()
    {
        if (IsDisposed || Disposing || LockRequested) return;
        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(new Action(RequestLifecycleLock));
            }
            catch (InvalidOperationException)
            {
            }
            return;
        }

        LockVault();
    }

    private static TimeSpan GetSystemIdleTime()
    {
        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info)) return TimeSpan.Zero;
        var elapsed = unchecked((uint)Environment.TickCount - info.TickCount);
        return TimeSpan.FromMilliseconds(elapsed);
    }

    private void OpenHashTool()
    {
        using var form = new PasswordHashToolForm();
        form.ShowDialog(this);
    }

    private void LockVault()
    {
        if (LockRequested) return;
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
        historyButton.Enabled = selected && item.Type == VaultItemType.Password;
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

    private sealed record TrashRow(Guid Id, string Title, DateTimeOffset? DeletedAt)
    {
        public override string ToString() => $"{Title} — deleted {DeletedAt?.ToLocalTime():g}";
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint Size;
        public uint TickCount;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref LastInputInfo info);
}
