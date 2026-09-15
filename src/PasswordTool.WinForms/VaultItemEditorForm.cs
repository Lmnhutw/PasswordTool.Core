using System.Runtime.InteropServices;
using PasswordTool.Core.Models;
using PasswordTool.Core.Services;

namespace PasswordTool.WinForms;

public sealed class VaultItemEditorForm : Form
{
    private readonly VaultItem originalItem;
    private readonly bool isEdit;
    private readonly List<string> recoveryCodes = [];
    private readonly ComboBox typeComboBox = new();
    private readonly TextBox titleTextBox = new();
    private readonly TextBox usernameTextBox = new();
    private readonly TextBox passwordTextBox = new();
    private readonly TextBox recoveryCodesTextBox = new();
    private readonly TextBox urlTextBox = new();
    private readonly TextBox notesTextBox = new();
    private readonly CheckBox showPasswordCheckBox = new();
    private readonly CheckBox hideUrlCheckBox = new();
    private readonly CheckBox hideNotesCheckBox = new();
    private readonly Label passwordLabel = CreateLabel("Password");
    private readonly Label recoveryCodesLabel = CreateLabel("Codes");
    private readonly Button pasteRecoveryCodesButton = new();
    private TableLayoutPanel layout = null!;

    public VaultItemEditorForm(VaultItem? item = null)
    {
        originalItem = item ?? new VaultItem();
        isEdit = item is not null;
        Item = Clone(originalItem);
        BuildInterface();
        LoadItem();
        FormIconService.Apply(this);
    }

    public VaultItem Item { get; private set; }

    public bool DeleteRequested { get; private set; }

    private void BuildInterface()
    {
        Text = isEdit ? "Edit Vault Item" : "Add Vault Item";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(700, 650);
        Padding = new Padding(16);

        layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 12
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        typeComboBox.Dock = DockStyle.Left;
        typeComboBox.Width = 260;
        typeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        typeComboBox.DisplayMember = nameof(VaultItemTypeOption.DisplayName);
        typeComboBox.Items.Add(new VaultItemTypeOption(VaultItemType.Password, "Password"));
        typeComboBox.Items.Add(new VaultItemTypeOption(VaultItemType.RecoveryCodes, "Recovery codes"));
        typeComboBox.SelectedIndexChanged += (_, _) => UpdateTypeInterface();

        titleTextBox.Dock = DockStyle.Fill;
        usernameTextBox.Dock = DockStyle.Fill;
        passwordTextBox.Dock = DockStyle.Fill;
        passwordTextBox.UseSystemPasswordChar = true;
        recoveryCodesTextBox.Dock = DockStyle.Fill;
        recoveryCodesTextBox.Multiline = true;
        recoveryCodesTextBox.ReadOnly = true;
        recoveryCodesTextBox.ScrollBars = ScrollBars.Vertical;
        recoveryCodesTextBox.ShortcutsEnabled = false;
        urlTextBox.Dock = DockStyle.Fill;
        notesTextBox.Dock = DockStyle.Fill;
        notesTextBox.Multiline = true;
        notesTextBox.ScrollBars = ScrollBars.Vertical;

        pasteRecoveryCodesButton.Text = "Paste from Clipboard && Review";
        pasteRecoveryCodesButton.AutoSize = true;
        pasteRecoveryCodesButton.Click += PasteRecoveryCodesButton_Click;

        hideUrlCheckBox.Text = "Hide URL in the vault list";
        hideUrlCheckBox.AutoSize = true;
        hideNotesCheckBox.Text = "Hide notes in the vault list";
        hideNotesCheckBox.AutoSize = true;

        showPasswordCheckBox.Text = "Show";
        showPasswordCheckBox.AutoSize = true;
        showPasswordCheckBox.Anchor = AnchorStyles.Left;
        showPasswordCheckBox.Margin = new Padding(0, 4, 0, 0);
        showPasswordCheckBox.CheckedChanged += (_, _) => passwordTextBox.UseSystemPasswordChar = !showPasswordCheckBox.Checked;

        var saveButton = new Button { Text = "Save", Width = 100, DialogResult = DialogResult.None };
        saveButton.Click += SaveButton_Click;
        var deleteButton = new Button { Text = "Delete", Width = 100, Visible = isEdit, DialogResult = DialogResult.None };
        deleteButton.Click += DeleteButton_Click;
        var cancelButton = new Button { Text = "Cancel", Width = 100, DialogResult = DialogResult.Cancel };

        var buttonRow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        buttonRow.Controls.Add(cancelButton);
        buttonRow.Controls.Add(saveButton);
        buttonRow.Controls.Add(deleteButton);

        layout.Controls.Add(CreateLabel("Item type"), 0, 0);
        layout.Controls.Add(typeComboBox, 1, 0);
        layout.Controls.Add(CreateLabel("Title"), 0, 1);
        layout.Controls.Add(titleTextBox, 1, 1);
        layout.Controls.Add(CreateLabel("Username"), 0, 2);
        layout.Controls.Add(usernameTextBox, 1, 2);
        layout.Controls.Add(passwordLabel, 0, 3);
        layout.Controls.Add(passwordTextBox, 1, 3);
        layout.Controls.Add(new Panel(), 0, 4);
        layout.Controls.Add(showPasswordCheckBox, 1, 4);
        layout.Controls.Add(new Panel(), 0, 5);
        layout.Controls.Add(pasteRecoveryCodesButton, 1, 5);
        layout.Controls.Add(recoveryCodesLabel, 0, 6);
        layout.Controls.Add(recoveryCodesTextBox, 1, 6);
        layout.Controls.Add(CreateLabel("URL"), 0, 7);
        layout.Controls.Add(urlTextBox, 1, 7);
        layout.Controls.Add(new Panel(), 0, 8);
        layout.Controls.Add(hideUrlCheckBox, 1, 8);
        layout.Controls.Add(CreateLabel("Notes"), 0, 9);
        layout.Controls.Add(notesTextBox, 1, 9);
        layout.Controls.Add(new Panel(), 0, 10);
        layout.Controls.Add(hideNotesCheckBox, 1, 10);
        layout.Controls.Add(buttonRow, 0, 11);
        layout.SetColumnSpan(buttonRow, 2);

        Controls.Add(layout);
        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    private void LoadItem()
    {
        var option = typeComboBox.Items.Cast<VaultItemTypeOption>()
            .Single(candidate => candidate.Type == originalItem.Type);
        typeComboBox.SelectedItem = option;
        titleTextBox.Text = originalItem.Title;
        usernameTextBox.Text = originalItem.Username;
        passwordTextBox.Text = originalItem.Password;
        recoveryCodes.AddRange(originalItem.RecoveryCodes ?? []);
        RenderRecoveryCodes();
        urlTextBox.Text = originalItem.Url;
        hideUrlCheckBox.Checked = originalItem.HideUrl;
        notesTextBox.Text = originalItem.Notes;
        hideNotesCheckBox.Checked = originalItem.HideNotes;
        UpdateTypeInterface();
    }

    private void UpdateTypeInterface()
    {
        var isRecoveryCodeItem = SelectedType == VaultItemType.RecoveryCodes;
        passwordLabel.Visible = !isRecoveryCodeItem;
        passwordTextBox.Visible = !isRecoveryCodeItem;
        showPasswordCheckBox.Visible = !isRecoveryCodeItem;
        pasteRecoveryCodesButton.Visible = isRecoveryCodeItem;
        recoveryCodesLabel.Visible = isRecoveryCodeItem;
        recoveryCodesTextBox.Visible = isRecoveryCodeItem;

        layout.RowStyles[3] = new RowStyle(SizeType.Absolute, isRecoveryCodeItem ? 0 : 38);
        layout.RowStyles[4] = new RowStyle(SizeType.Absolute, isRecoveryCodeItem ? 0 : 30);
        layout.RowStyles[5] = new RowStyle(SizeType.Absolute, isRecoveryCodeItem ? 40 : 0);
        layout.RowStyles[6] = new RowStyle(isRecoveryCodeItem ? SizeType.Percent : SizeType.Absolute, isRecoveryCodeItem ? 55 : 0);
        layout.RowStyles[9] = new RowStyle(SizeType.Percent, isRecoveryCodeItem ? 45 : 100);
    }

    private VaultItemType SelectedType => typeComboBox.SelectedItem is VaultItemTypeOption option
        ? option.Type
        : VaultItemType.Password;

    private void PasteRecoveryCodesButton_Click(object? sender, EventArgs e)
    {
        try
        {
            var result = RecoveryCodeParser.Parse(Clipboard.ContainsText() ? Clipboard.GetText() : null);
            if (!result.IsValid)
            {
                ShowWarning(result.ErrorMessage!);
                return;
            }

            using var reviewForm = new RecoveryCodesReviewForm(result.Codes);
            if (reviewForm.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            recoveryCodes.Clear();
            recoveryCodes.AddRange(result.Codes);
            RenderRecoveryCodes();
        }
        catch (ExternalException)
        {
            ShowWarning("Windows could not read the clipboard. Copy the recovery codes and try again.");
        }
    }

    private void RenderRecoveryCodes()
    {
        recoveryCodesTextBox.Text = string.Join(Environment.NewLine, recoveryCodes);
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(titleTextBox.Text))
        {
            ShowWarning("Title is required.");
            return;
        }

        if (SelectedType == VaultItemType.Password && string.IsNullOrWhiteSpace(passwordTextBox.Text))
        {
            ShowWarning("Password is required.");
            return;
        }

        if (SelectedType == VaultItemType.RecoveryCodes && recoveryCodes.Count < 2)
        {
            ShowWarning("Paste and review at least two recovery codes before saving.");
            return;
        }

        Item = new VaultItem
        {
            Id = originalItem.Id,
            Type = SelectedType,
            Title = titleTextBox.Text.Trim(),
            Username = usernameTextBox.Text.Trim(),
            Password = SelectedType == VaultItemType.Password ? passwordTextBox.Text : string.Empty,
            RecoveryCodes = SelectedType == VaultItemType.RecoveryCodes ? [.. recoveryCodes] : [],
            Url = urlTextBox.Text.Trim(),
            HideUrl = hideUrlCheckBox.Checked,
            Notes = notesTextBox.Text,
            HideNotes = hideNotesCheckBox.Checked,
            CreatedAt = originalItem.CreatedAt,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        DialogResult = DialogResult.OK;
        Close();
    }

    private void DeleteButton_Click(object? sender, EventArgs e)
    {
        if (MessageBox.Show("Delete this vault item?", "PasswordTool", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        DeleteRequested = true;
        DialogResult = DialogResult.OK;
        Close();
    }

    private static VaultItem Clone(VaultItem item)
    {
        return new VaultItem
        {
            Id = item.Id,
            Type = item.Type,
            Title = item.Title,
            Username = item.Username,
            Password = item.Password,
            RecoveryCodes = [.. (item.RecoveryCodes ?? [])],
            Url = item.Url,
            HideUrl = item.HideUrl,
            Notes = item.Notes,
            HideNotes = item.HideNotes,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        };
    }

    private static Label CreateLabel(string text)
    {
        return new Label { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    }

    private static void ShowWarning(string message)
    {
        MessageBox.Show(message, "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private sealed record VaultItemTypeOption(VaultItemType Type, string DisplayName);
}
