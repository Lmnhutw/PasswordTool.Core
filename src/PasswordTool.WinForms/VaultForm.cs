using System.Runtime.InteropServices;
using System.Security.Cryptography;
using PasswordTool.Core.Models;
using PasswordTool.Core.Services;

namespace PasswordTool.WinForms;

public sealed class VaultForm : Form
{
    private readonly VaultService vaultService;
    private readonly DataGridView itemsGrid = new();
    private readonly Button editButton = new();
    private readonly Button deleteButton = new();
    private readonly Button viewSecretButton = new();

    public VaultForm(VaultService vaultService)
    {
        this.vaultService = vaultService;
        BuildInterface();
        LoadItems();
        FormIconService.Apply(this);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        vaultService.ClearSession();
        base.OnFormClosed(e);
    }

    private void BuildInterface()
    {
        Text = "Password Vault";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(1050, 680);
        MinimumSize = new Size(900, 560);
        Padding = new Padding(12);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };

        var addButton = CreateToolbarButton("Add");
        addButton.Click += AddButton_Click;

        var hashToolButton = CreateToolbarButton("Hash Tool");
        hashToolButton.Click += HashToolButton_Click;

        var settingsButton = CreateToolbarButton("Settings");
        settingsButton.Click += SettingsButton_Click;

        var exportButton = CreateToolbarButton("Export JSON");
        exportButton.Click += ExportButton_Click;

        var importButton = CreateToolbarButton("Import JSON");
        importButton.Click += ImportButton_Click;

        editButton.Text = "Edit";
        editButton.Width = 90;
        editButton.Click += EditButton_Click;

        deleteButton.Text = "Delete";
        deleteButton.Width = 90;
        deleteButton.Click += DeleteButton_Click;

        viewSecretButton.Text = "View Secret";
        viewSecretButton.Width = 110;
        viewSecretButton.Click += ViewSecretButton_Click;

        toolbar.Controls.Add(addButton);
        toolbar.Controls.Add(hashToolButton);
        toolbar.Controls.Add(settingsButton);
        toolbar.Controls.Add(exportButton);
        toolbar.Controls.Add(importButton);
        toolbar.Controls.Add(editButton);
        toolbar.Controls.Add(deleteButton);
        toolbar.Controls.Add(viewSecretButton);

        ConfigureGrid();

        root.Controls.Add(toolbar, 0, 0);
        root.Controls.Add(itemsGrid, 0, 1);
        Controls.Add(root);
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

        itemsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Title", HeaderText = "Title", FillWeight = 20 });
        itemsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Type", HeaderText = "Type", FillWeight = 13 });
        itemsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Username", HeaderText = "Username", FillWeight = 18 });
        itemsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Secret", HeaderText = "Secret", FillWeight = 12 });
        itemsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Url", HeaderText = "Url", FillWeight = 20 });
        itemsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Notes", HeaderText = "Notes", FillWeight = 18 });
        itemsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "CreatedAt", HeaderText = "Created", FillWeight = 12 });
        itemsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "UpdatedAt", HeaderText = "Updated", FillWeight = 12 });
    }

    private void LoadItems()
    {
        itemsGrid.Rows.Clear();

        foreach (var item in vaultService.GetItems())
        {
            var rowIndex = itemsGrid.Rows.Add(
                item.Title,
                item.Type == VaultItemType.Password ? "Password" : "Recovery codes",
                item.Username,
                item.Type == VaultItemType.Password ? "********" : $"{item.RecoveryCodeCount} codes",
                item.HideUrl ? "Hidden" : item.Url,
                item.HideNotes ? "Hidden" : item.Notes,
                item.CreatedAt.ToLocalTime().ToString("g"),
                item.UpdatedAt.ToLocalTime().ToString("g"));

            itemsGrid.Rows[rowIndex].Tag = new VaultItemRowTag(item.Id, item.Type);
        }

        UpdateButtonState();
    }

    private void HashToolButton_Click(object? sender, EventArgs e)
    {
        using var hashToolForm = new PasswordHashToolForm();
        hashToolForm.ShowDialog(this);
    }

    private void SettingsButton_Click(object? sender, EventArgs e)
    {
        using var settings = new VaultSettingsForm(vaultService);
        settings.ShowDialog(this);
    }

    private void AddButton_Click(object? sender, EventArgs e)
    {
        using var editor = new VaultItemEditorForm();
        if (editor.ShowDialog(this) != DialogResult.OK || editor.DeleteRequested)
        {
            return;
        }

        RunVaultAction(() =>
        {
            vaultService.AddItem(editor.Item);
            LoadItems();
        });
    }

    private void EditButton_Click(object? sender, EventArgs e)
    {
        EditSelectedItem();
    }

    private void EditSelectedItem()
    {
        if (!TryGetSelectedItemId(out var itemId))
        {
            return;
        }

        var code = RequestTotpCode("Edit Vault Item", "Enter your Google Authenticator code before editing this item.");
        if (code is null)
        {
            return;
        }

        RunVaultAction(() =>
        {
            var item = vaultService.GetItemForEditing(itemId, code);
            using var editor = new VaultItemEditorForm(item);

            if (editor.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            if (editor.DeleteRequested)
            {
                vaultService.DeleteItem(itemId);
            }
            else
            {
                vaultService.UpdateItem(editor.Item);
            }

            LoadItems();
        });
    }

    private void DeleteButton_Click(object? sender, EventArgs e)
    {
        if (!TryGetSelectedItemId(out var itemId))
        {
            return;
        }

        if (MessageBox.Show("Delete the selected vault item?", "PasswordTool", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        RunVaultAction(() =>
        {
            vaultService.DeleteItem(itemId);
            LoadItems();
        });
    }

    private void ViewSecretButton_Click(object? sender, EventArgs e)
    {
        if (!TryGetSelectedItem(out var selectedItem))
        {
            return;
        }

        var isPassword = selectedItem.Type == VaultItemType.Password;
        var code = RequestTotpCode(
            isPassword ? "View Password" : "View Recovery Codes",
            isPassword
                ? "Enter your Google Authenticator code before viewing this password."
                : "Enter your Google Authenticator code before viewing these recovery codes.");
        if (code is null)
        {
            return;
        }

        RunVaultAction(() =>
        {
            if (isPassword)
            {
                ShowPasswordDialog(vaultService.GetPassword(selectedItem.Id, code));
            }
            else
            {
                ShowRecoveryCodesDialog(vaultService.GetRecoveryCodes(selectedItem.Id, code));
            }
        });
    }

    private void ExportButton_Click(object? sender, EventArgs e)
    {
        using var passphraseForm = new BackupPassphraseForm(requireConfirmation: true);
        if (passphraseForm.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        using var saveDialog = new SaveFileDialog
        {
            Title = "Export Encrypted PasswordTool Backup",
            Filter = "PasswordTool JSON backup (*.json)|*.json",
            AddExtension = true,
            DefaultExt = "json",
            FileName = $"PasswordTool-backup-{DateTime.Now:yyyy-MM-dd}.json",
            OverwritePrompt = true
        };
        if (saveDialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var code = RequestTotpCode("Export JSON Backup", "Enter your Google Authenticator code before exporting vault secrets.");
        if (code is null)
        {
            return;
        }

        RunVaultAction(() =>
        {
            var backupJson = vaultService.ExportBackupJson(passphraseForm.Passphrase, code);
            File.WriteAllText(saveDialog.FileName, backupJson);
            MessageBox.Show("The encrypted JSON backup was exported successfully.", "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
    }

    private void ImportButton_Click(object? sender, EventArgs e)
    {
        using var openDialog = new OpenFileDialog
        {
            Title = "Import PasswordTool JSON Backup",
            Filter = "PasswordTool JSON backup (*.json)|*.json",
            CheckFileExists = true,
            Multiselect = false
        };
        if (openDialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        string backupJson;
        try
        {
            var fileInfo = new FileInfo(openDialog.FileName);
            if (fileInfo.Length > VaultBackupService.MaxBackupJsonCharacters)
            {
                throw new InvalidDataException("The selected backup exceeds the 10 MB limit.");
            }

            backupJson = File.ReadAllText(openDialog.FileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(ex.Message, "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        using var passphraseForm = new BackupPassphraseForm(requireConfirmation: false);
        if (passphraseForm.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var code = RequestTotpCode("Import JSON Backup", "Enter your Google Authenticator code before reviewing imported vault items.");
        if (code is null)
        {
            return;
        }

        RunVaultAction(() =>
        {
            var plan = vaultService.PreviewBackupImport(backupJson, passphraseForm.Passphrase, code);
            using var reviewForm = new VaultImportReviewForm(plan);
            if (reviewForm.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            var importedCount = vaultService.ImportBackupJson(backupJson, passphraseForm.Passphrase, code);
            LoadItems();
            MessageBox.Show($"Imported {importedCount} new vault item(s). Existing items were not overwritten.", "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
    }

    private string? RequestTotpCode(string title, string prompt)
    {
        if (!vaultService.IsGoogleAuthenticatorConfigured)
        {
            return string.Empty;
        }

        using var verifyTotpForm = new VerifyTotpForm(title, prompt, vaultService.VerifyTotpForSensitiveAction);
        return verifyTotpForm.ShowDialog(this) == DialogResult.OK
            ? verifyTotpForm.Code
            : null;
    }

    private void ShowPasswordDialog(string password)
    {
        using var dialog = new Form
        {
            Text = "Password",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ClientSize = new Size(520, 130),
            Padding = new Padding(16)
        };
        FormIconService.Apply(dialog);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        var passwordTextBox = new TextBox
        {
            Text = password,
            Dock = DockStyle.Fill,
            ReadOnly = true
        };
        passwordTextBox.ShortcutsEnabled = false;

        var closeButton = new Button
        {
            Text = "Close",
            Width = 100,
            DialogResult = DialogResult.OK
        };

        var buttonRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft
        };
        buttonRow.Controls.Add(closeButton);

        layout.Controls.Add(passwordTextBox, 0, 0);
        layout.Controls.Add(buttonRow, 0, 1);
        dialog.Controls.Add(layout);
        dialog.AcceptButton = closeButton;

        dialog.ShowDialog(this);
    }

    private void ShowRecoveryCodesDialog(IReadOnlyList<string> recoveryCodes)
    {
        using var dialog = new Form
        {
            Text = "Recovery Codes",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ClientSize = new Size(560, 420),
            Padding = new Padding(16)
        };
        FormIconService.Apply(dialog);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        var heading = new Label
        {
            Text = $"{recoveryCodes.Count} recovery codes",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        var codesTextBox = new TextBox
        {
            Text = string.Join(Environment.NewLine, recoveryCodes),
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            ShortcutsEnabled = false
        };
        var closeButton = new Button { Text = "Close", Width = 100, DialogResult = DialogResult.OK };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.Add(closeButton);

        layout.Controls.Add(heading, 0, 0);
        layout.Controls.Add(codesTextBox, 0, 1);
        layout.Controls.Add(buttons, 0, 2);
        dialog.Controls.Add(layout);
        dialog.AcceptButton = closeButton;
        dialog.ShowDialog(this);
    }

    private bool TryGetSelectedItemId(out Guid itemId)
    {
        itemId = Guid.Empty;

        if (!TryGetSelectedItem(out var selectedItem))
        {
            return false;
        }

        itemId = selectedItem.Id;
        return true;
    }

    private bool TryGetSelectedItem(out VaultItemRowTag selectedItem)
    {
        selectedItem = null!;
        if (itemsGrid.CurrentRow?.Tag is not VaultItemRowTag rowTag)
        {
            return false;
        }

        selectedItem = rowTag;
        return true;
    }

    private void UpdateButtonState()
    {
        var hasSelection = TryGetSelectedItemId(out _);
        editButton.Enabled = hasSelection;
        deleteButton.Enabled = hasSelection;
        viewSecretButton.Enabled = hasSelection;
        if (hasSelection && TryGetSelectedItem(out var selectedItem))
        {
            viewSecretButton.Text = selectedItem.Type == VaultItemType.Password ? "View Password" : "View Codes";
        }
        else
        {
            viewSecretButton.Text = "View Secret";
        }
    }

    private static Button CreateToolbarButton(string text)
    {
        return new Button
        {
            Text = text,
            Width = 95,
            Height = 32
        };
    }

    private static void RunVaultAction(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (ex is ArgumentException
            or InvalidOperationException
            or UnauthorizedAccessException
            or IOException
            or CryptographicException
            or ExternalException)
        {
            MessageBox.Show(ex.Message, "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void VaultForm_Load(object sender, EventArgs e)
    {

    }

    private sealed record VaultItemRowTag(Guid Id, VaultItemType Type);
}
