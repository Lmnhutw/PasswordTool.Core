using PasswordTool.Core.Models;

namespace PasswordTool.WinForms;

public sealed class VaultItemEditorForm : Form
{
    private readonly VaultItem originalItem;
    private readonly bool isEdit;
    private readonly TextBox titleTextBox = new();
    private readonly TextBox usernameTextBox = new();
    private readonly TextBox passwordTextBox = new();
    private readonly TextBox urlTextBox = new();
    private readonly TextBox notesTextBox = new();
    private readonly CheckBox showPasswordCheckBox = new();

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
        ClientSize = new Size(640, 480);
        Padding = new Padding(16);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 8
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        titleTextBox.Dock = DockStyle.Fill;
        usernameTextBox.Dock = DockStyle.Fill;
        passwordTextBox.Dock = DockStyle.Fill;
        passwordTextBox.UseSystemPasswordChar = true;
        urlTextBox.Dock = DockStyle.Fill;
        notesTextBox.Dock = DockStyle.Fill;
        notesTextBox.Multiline = true;
        notesTextBox.ScrollBars = ScrollBars.Vertical;

        showPasswordCheckBox.Text = "Show";
        showPasswordCheckBox.AutoSize = true;
        showPasswordCheckBox.Anchor = AnchorStyles.Left;
        showPasswordCheckBox.Margin = new Padding(0, 4, 0, 0);
        showPasswordCheckBox.CheckedChanged += (_, _) => passwordTextBox.UseSystemPasswordChar = !showPasswordCheckBox.Checked;

        var saveButton = new Button
        {
            Text = "Save",
            Width = 100,
            DialogResult = DialogResult.None
        };
        saveButton.Click += SaveButton_Click;

        var deleteButton = new Button
        {
            Text = "Delete",
            Width = 100,
            Visible = isEdit,
            DialogResult = DialogResult.None
        };
        deleteButton.Click += DeleteButton_Click;

        var cancelButton = new Button
        {
            Text = "Cancel",
            Width = 100,
            DialogResult = DialogResult.Cancel
        };

        var buttonRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft
        };
        buttonRow.Controls.Add(cancelButton);
        buttonRow.Controls.Add(saveButton);
        buttonRow.Controls.Add(deleteButton);

        layout.Controls.Add(CreateLabel("Title"), 0, 0);
        layout.Controls.Add(titleTextBox, 1, 0);
        layout.Controls.Add(CreateLabel("Username"), 0, 1);
        layout.Controls.Add(usernameTextBox, 1, 1);
        layout.Controls.Add(CreateLabel("Password"), 0, 2);
        layout.Controls.Add(passwordTextBox, 1, 2);
        layout.Controls.Add(new Panel(), 0, 3);
        layout.Controls.Add(showPasswordCheckBox, 1, 3);
        layout.Controls.Add(CreateLabel("Url"), 0, 4);
        layout.Controls.Add(urlTextBox, 1, 4);
        layout.Controls.Add(CreateLabel("Notes"), 0, 5);
        layout.Controls.Add(notesTextBox, 1, 5);
        layout.Controls.Add(buttonRow, 0, 7);
        layout.SetColumnSpan(buttonRow, 2);

        Controls.Add(layout);
        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    private void LoadItem()
    {
        titleTextBox.Text = originalItem.Title;
        usernameTextBox.Text = originalItem.Username;
        passwordTextBox.Text = originalItem.Password;
        urlTextBox.Text = originalItem.Url;
        notesTextBox.Text = originalItem.Notes;
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(titleTextBox.Text))
        {
            ShowWarning("Title is required.");
            return;
        }

        if (string.IsNullOrWhiteSpace(passwordTextBox.Text))
        {
            ShowWarning("Password is required.");
            return;
        }

        Item = new VaultItem
        {
            Id = originalItem.Id,
            Title = titleTextBox.Text.Trim(),
            Username = usernameTextBox.Text.Trim(),
            Password = passwordTextBox.Text,
            Url = urlTextBox.Text.Trim(),
            Notes = notesTextBox.Text,
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
            Title = item.Title,
            Username = item.Username,
            Password = item.Password,
            Url = item.Url,
            Notes = item.Notes,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        };
    }

    private static Label CreateLabel(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static void ShowWarning(string message)
    {
        MessageBox.Show(message, "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
