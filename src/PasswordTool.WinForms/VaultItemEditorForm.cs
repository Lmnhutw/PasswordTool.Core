using System.Runtime.InteropServices;
using PasswordTool.Core.Models;
using PasswordTool.Core.Services;

namespace PasswordTool.WinForms;

public sealed class VaultItemEditorForm : Form
{
    private readonly VaultItem originalItem;
    private readonly bool isEdit;
    private readonly TotpService totpService = new();
    private readonly List<string> recoveryCodes = [];
    private readonly ComboBox typeComboBox = new();
    private readonly TextBox titleTextBox = new();
    private readonly TextBox usernameTextBox = new();
    private readonly TextBox passwordTextBox = new();
    private readonly TextBox totpSecretTextBox = new();
    private readonly TextBox recoveryCodesTextBox = new();
    private readonly TextBox urlTextBox = new();
    private readonly TextBox folderTextBox = new();
    private readonly TextBox tagsTextBox = new();
    private readonly TextBox notesTextBox = new();
    private readonly CheckBox favoriteCheckBox = new() { Text = "Favorite", AutoSize = true };
    private readonly CheckBox showPasswordCheckBox = new() { Text = "Show password", AutoSize = true };
    private readonly CheckBox showTotpSecretCheckBox = new() { Text = "Show website TOTP secret", AutoSize = true };
    private readonly CheckBox hideUrlCheckBox = new() { Text = "Hide URL in the vault list", AutoSize = true };
    private readonly CheckBox hideNotesCheckBox = new() { Text = "Hide notes in the vault list", AutoSize = true };
    private readonly Label passwordLabel = CreateLabel("Password");
    private readonly Label totpLabel = CreateLabel("Website TOTP");
    private readonly Label recoveryCodesLabel = CreateLabel("Codes");
    private readonly Button pasteRecoveryCodesButton = new() { Text = "Paste from Clipboard && Review", AutoSize = true };
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
        ClientSize = new Size(780, 800);
        Padding = new Padding(16);

        layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 16 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var height in new[] { 38, 38, 38, 38, 30, 38, 38, 38, 30, 40 })
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        }
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        typeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        typeComboBox.DisplayMember = nameof(VaultItemTypeOption.DisplayName);
        typeComboBox.Width = 260;
        typeComboBox.Items.Add(new VaultItemTypeOption(VaultItemType.Password, "Password"));
        typeComboBox.Items.Add(new VaultItemTypeOption(VaultItemType.RecoveryCodes, "Recovery codes"));
        typeComboBox.SelectedIndexChanged += (_, _) => UpdateTypeInterface();

        ConfigureTextBox(titleTextBox);
        ConfigureTextBox(usernameTextBox);
        ConfigureTextBox(urlTextBox);
        ConfigureTextBox(folderTextBox);
        ConfigureTextBox(tagsTextBox);
        ConfigureTextBox(passwordTextBox);
        passwordTextBox.UseSystemPasswordChar = true;
        ConfigureTextBox(totpSecretTextBox);
        totpSecretTextBox.UseSystemPasswordChar = true;

        notesTextBox.Dock = DockStyle.Fill;
        notesTextBox.Multiline = true;
        notesTextBox.ScrollBars = ScrollBars.Vertical;
        recoveryCodesTextBox.Dock = DockStyle.Fill;
        recoveryCodesTextBox.Multiline = true;
        recoveryCodesTextBox.ReadOnly = true;
        recoveryCodesTextBox.ScrollBars = ScrollBars.Vertical;
        recoveryCodesTextBox.ShortcutsEnabled = false;

        var generateButton = new Button { Text = "Generate", Width = 100 };
        generateButton.Click += GenerateButton_Click;
        var passwordRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        passwordRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        passwordRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108));
        passwordRow.Controls.Add(passwordTextBox, 0, 0);
        passwordRow.Controls.Add(generateButton, 1, 0);
        var totpRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        totpRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        totpRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        totpRow.Controls.Add(totpSecretTextBox, 0, 0);
        totpRow.Controls.Add(showTotpSecretCheckBox, 1, 0);

        showPasswordCheckBox.CheckedChanged += (_, _) => passwordTextBox.UseSystemPasswordChar = !showPasswordCheckBox.Checked;
        showTotpSecretCheckBox.CheckedChanged += (_, _) => totpSecretTextBox.UseSystemPasswordChar = !showTotpSecretCheckBox.Checked;
        pasteRecoveryCodesButton.Click += PasteRecoveryCodesButton_Click;

        var saveButton = new Button { Text = "Save", Width = 100 };
        saveButton.Click += SaveButton_Click;
        var deleteButton = new Button { Text = "Delete", Width = 100, Visible = isEdit };
        deleteButton.Click += DeleteButton_Click;
        var cancelButton = new Button { Text = "Cancel", Width = 100, DialogResult = DialogResult.Cancel };
        var buttonRow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        buttonRow.Controls.Add(cancelButton);
        buttonRow.Controls.Add(saveButton);
        buttonRow.Controls.Add(deleteButton);

        AddRow("Item type", typeComboBox, 0);
        AddRow("Title", titleTextBox, 1);
        AddRow("Username", usernameTextBox, 2);
        layout.Controls.Add(passwordLabel, 0, 3);
        layout.Controls.Add(passwordRow, 1, 3);
        AddRow(string.Empty, showPasswordCheckBox, 4);
        layout.Controls.Add(totpLabel, 0, 5);
        layout.Controls.Add(totpRow, 1, 5);
        AddRow("Folder", folderTextBox, 6);
        AddRow("Tags", tagsTextBox, 7);
        AddRow(string.Empty, favoriteCheckBox, 8);
        AddRow(string.Empty, pasteRecoveryCodesButton, 9);
        layout.Controls.Add(recoveryCodesLabel, 0, 10);
        layout.Controls.Add(recoveryCodesTextBox, 1, 10);
        AddRow("URL", urlTextBox, 11);
        AddRow(string.Empty, hideUrlCheckBox, 12);
        AddRow("Notes", notesTextBox, 13);
        AddRow(string.Empty, hideNotesCheckBox, 14);
        layout.Controls.Add(buttonRow, 0, 15);
        layout.SetColumnSpan(buttonRow, 2);

        Controls.Add(layout);
        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    private void AddRow(string label, Control control, int row)
    {
        layout.Controls.Add(string.IsNullOrEmpty(label) ? new Panel() : CreateLabel(label), 0, row);
        layout.Controls.Add(control, 1, row);
    }

    private static void ConfigureTextBox(TextBox textBox) => textBox.Dock = DockStyle.Fill;

    private void LoadItem()
    {
        typeComboBox.SelectedItem = typeComboBox.Items.Cast<VaultItemTypeOption>().Single(option => option.Type == originalItem.Type);
        titleTextBox.Text = originalItem.Title;
        usernameTextBox.Text = originalItem.Username;
        passwordTextBox.Text = originalItem.Password;
        totpSecretTextBox.Text = originalItem.TotpSecretBase32;
        folderTextBox.Text = originalItem.Folder;
        tagsTextBox.Text = string.Join(", ", originalItem.Tags ?? []);
        favoriteCheckBox.Checked = originalItem.IsFavorite;
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
        if (layout is null) return;
        var recovery = SelectedType == VaultItemType.RecoveryCodes;
        foreach (Control control in new Control[] { passwordLabel, passwordTextBox, showPasswordCheckBox, totpLabel, totpSecretTextBox })
        {
            control.Visible = !recovery;
        }
        layout.GetControlFromPosition(1, 3)!.Visible = !recovery;
        pasteRecoveryCodesButton.Visible = recovery;
        recoveryCodesLabel.Visible = recovery;
        recoveryCodesTextBox.Visible = recovery;
        layout.RowStyles[3].Height = recovery ? 0 : 38;
        layout.RowStyles[4].Height = recovery ? 0 : 30;
        layout.RowStyles[5].Height = recovery ? 0 : 38;
        layout.RowStyles[9].Height = recovery ? 40 : 0;
        layout.RowStyles[10] = new RowStyle(recovery ? SizeType.Percent : SizeType.Absolute, recovery ? 45 : 0);
        layout.RowStyles[13] = new RowStyle(SizeType.Percent, recovery ? 55 : 100);
    }

    private VaultItemType SelectedType => typeComboBox.SelectedItem is VaultItemTypeOption option ? option.Type : VaultItemType.Password;

    private void GenerateButton_Click(object? sender, EventArgs e)
    {
        using var generatorForm = new PasswordGeneratorForm();
        if (generatorForm.ShowDialog(this) == DialogResult.OK)
        {
            passwordTextBox.Text = generatorForm.GeneratedValue;
        }
    }

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
            if (reviewForm.ShowDialog(this) != DialogResult.OK) return;
            recoveryCodes.Clear();
            recoveryCodes.AddRange(result.Codes);
            RenderRecoveryCodes();
        }
        catch (ExternalException)
        {
            ShowWarning("Windows could not read the clipboard. Copy the recovery codes and try again.");
        }
    }

    private void RenderRecoveryCodes() => recoveryCodesTextBox.Text = string.Join(Environment.NewLine, recoveryCodes);

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

        var normalizedTotp = string.Empty;
        if (SelectedType == VaultItemType.Password && !string.IsNullOrWhiteSpace(totpSecretTextBox.Text)
            && !totpService.TryNormalizeWebsiteSecret(totpSecretTextBox.Text, out normalizedTotp))
        {
            ShowWarning("Enter a valid Base32 website TOTP secret or otpauth URI.");
            return;
        }

        Item = new VaultItem
        {
            Id = originalItem.Id,
            Type = SelectedType,
            Title = titleTextBox.Text.Trim(),
            Username = usernameTextBox.Text.Trim(),
            Password = SelectedType == VaultItemType.Password ? passwordTextBox.Text : string.Empty,
            TotpSecretBase32 = normalizedTotp,
            RecoveryCodes = SelectedType == VaultItemType.RecoveryCodes ? [.. recoveryCodes] : [],
            Url = urlTextBox.Text.Trim(),
            HideUrl = hideUrlCheckBox.Checked,
            Notes = notesTextBox.Text,
            HideNotes = hideNotesCheckBox.Checked,
            IsFavorite = favoriteCheckBox.Checked,
            Folder = folderTextBox.Text.Trim(),
            Tags = tagsTextBox.Text.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            CreatedAt = originalItem.CreatedAt,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        DialogResult = DialogResult.OK;
        Close();
    }

    private void DeleteButton_Click(object? sender, EventArgs e)
    {
        if (MessageBox.Show("Delete this vault item?", "PasswordTool", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        DeleteRequested = true;
        DialogResult = DialogResult.OK;
        Close();
    }

    private static VaultItem Clone(VaultItem item) => new()
    {
        Id = item.Id,
        Type = item.Type,
        Title = item.Title,
        Username = item.Username,
        Password = item.Password,
        TotpSecretBase32 = item.TotpSecretBase32,
        RecoveryCodes = [.. (item.RecoveryCodes ?? [])],
        Url = item.Url,
        HideUrl = item.HideUrl,
        Notes = item.Notes,
        HideNotes = item.HideNotes,
        IsFavorite = item.IsFavorite,
        Folder = item.Folder,
        Tags = [.. (item.Tags ?? [])],
        CreatedAt = item.CreatedAt,
        UpdatedAt = item.UpdatedAt,
        PasswordChangedAt = item.PasswordChangedAt
    };

    private static Label CreateLabel(string text) => new() { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };

    private static void ShowWarning(string message) => MessageBox.Show(message, "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Warning);

    private sealed record VaultItemTypeOption(VaultItemType Type, string DisplayName);
}
