using System.Runtime.InteropServices;
using PasswordTool.Core.Models;
using PasswordTool.Core.Services;

namespace PasswordTool.WinForms;

public sealed class VaultForm : Form
{
    private readonly VaultService vaultService;
    private readonly DataGridView itemsGrid = new();
    private readonly Button editButton = new();
    private readonly Button deleteButton = new();
    private readonly Button copyUsernameButton = new();
    private readonly Button copyPasswordButton = new();
    private readonly Button viewPasswordButton = new();

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

        editButton.Text = "Edit";
        editButton.Width = 110;
        editButton.Click += EditButton_Click;

        deleteButton.Text = "Delete";
        deleteButton.Width = 110;
        deleteButton.Click += DeleteButton_Click;

        copyUsernameButton.Text = "Copy Username";
        copyUsernameButton.Width = 130;
        copyUsernameButton.Click += CopyUsernameButton_Click;

        copyPasswordButton.Text = "Copy Password";
        copyPasswordButton.Width = 130;
        copyPasswordButton.Click += CopyPasswordButton_Click;

        viewPasswordButton.Text = "View Password";
        viewPasswordButton.Width = 130;
        viewPasswordButton.Click += ViewPasswordButton_Click;

        toolbar.Controls.Add(addButton);
        toolbar.Controls.Add(hashToolButton);
        toolbar.Controls.Add(editButton);
        toolbar.Controls.Add(deleteButton);
        toolbar.Controls.Add(copyUsernameButton);
        toolbar.Controls.Add(copyPasswordButton);
        toolbar.Controls.Add(viewPasswordButton);

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
        itemsGrid.MultiSelect = false;
        itemsGrid.ReadOnly = true;
        itemsGrid.RowHeadersVisible = false;
        itemsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        itemsGrid.SelectionChanged += (_, _) => UpdateButtonState();
        itemsGrid.CellDoubleClick += (_, _) => EditSelectedItem();

        itemsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Title", HeaderText = "Title", FillWeight = 22 });
        itemsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Username", HeaderText = "Username", FillWeight = 18 });
        itemsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Password", HeaderText = "Password", FillWeight = 12 });
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
                item.Username,
                "********",
                item.Url,
                item.Notes,
                item.CreatedAt.ToLocalTime().ToString("g"),
                item.UpdatedAt.ToLocalTime().ToString("g"));

            itemsGrid.Rows[rowIndex].Tag = item.Id;
        }

        UpdateButtonState();
    }

    private void HashToolButton_Click(object? sender, EventArgs e)
    {
        using var hashToolForm = new PasswordHashToolForm();
        hashToolForm.ShowDialog(this);
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

        var code = RequestTotpCode("Edit Vault Item", "Enter your Authenticator code before editing this item.");
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

    private void CopyUsernameButton_Click(object? sender, EventArgs e)
    {
        if (!TryGetSelectedItemId(out var itemId))
        {
            return;
        }

        RunVaultAction(() =>
        {
            SetClipboardText(vaultService.GetUsername(itemId));
            MessageBox.Show("Username copied.", "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
    }

    private void CopyPasswordButton_Click(object? sender, EventArgs e)
    {
        if (!TryGetSelectedItemId(out var itemId))
        {
            return;
        }

        var code = RequestTotpCode("Copy Password", "Enter your Authenticator code before copying this password.");
        if (code is null)
        {
            return;
        }

        RunVaultAction(() =>
        {
            SetClipboardText(vaultService.GetPassword(itemId, code));
            MessageBox.Show("Password copied.", "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
    }

    private void ViewPasswordButton_Click(object? sender, EventArgs e)
    {
        if (!TryGetSelectedItemId(out var itemId))
        {
            return;
        }

        var code = RequestTotpCode("View Password", "Enter your Authenticator code before viewing this password.");
        if (code is null)
        {
            return;
        }

        RunVaultAction(() =>
        {
            var password = vaultService.GetPassword(itemId, code);
            ShowPasswordDialog(password);
        });
    }

    private string? RequestTotpCode(string title, string prompt)
    {
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

        var closeButton = new Button
        {
            Text = "Close",
            Width = 100,
            DialogResult = DialogResult.OK
        };

        var copyButton = new Button
        {
            Text = "Copy",
            Width = 100
        };
        copyButton.Click += (_, _) =>
        {
            SetClipboardText(password);
            MessageBox.Show("Password copied.", "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        var buttonRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft
        };
        buttonRow.Controls.Add(closeButton);
        buttonRow.Controls.Add(copyButton);

        layout.Controls.Add(passwordTextBox, 0, 0);
        layout.Controls.Add(buttonRow, 0, 1);
        dialog.Controls.Add(layout);
        dialog.AcceptButton = closeButton;

        dialog.ShowDialog(this);
    }

    private bool TryGetSelectedItemId(out Guid itemId)
    {
        itemId = Guid.Empty;

        if (itemsGrid.CurrentRow?.Tag is not Guid selectedId)
        {
            return false;
        }

        itemId = selectedId;
        return true;
    }

    private void UpdateButtonState()
    {
        var hasSelection = TryGetSelectedItemId(out _);
        editButton.Enabled = hasSelection;
        deleteButton.Enabled = hasSelection;
        copyUsernameButton.Enabled = hasSelection;
        copyPasswordButton.Enabled = hasSelection;
        viewPasswordButton.Enabled = hasSelection;
    }

    private static Button CreateToolbarButton(string text)
    {
        return new Button
        {
            Text = text,
            Width = 110,
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
            or ExternalException)
        {
            MessageBox.Show(ex.Message, "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void SetClipboardText(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            Clipboard.Clear();
            return;
        }

        Clipboard.SetText(value);
    }

    private void VaultForm_Load(object sender, EventArgs e)
    {

    }
}
