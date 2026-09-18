using PasswordTool.Core.Services;

namespace PasswordTool.WinForms;

public sealed class ChangeMasterPasswordForm : Form
{
    private readonly VaultService vaultService;
    private readonly TextBox currentPassword = CreatePasswordBox();
    private readonly TextBox newPassword = CreatePasswordBox();
    private readonly TextBox confirmation = CreatePasswordBox();

    public ChangeMasterPasswordForm(VaultService vaultService)
    {
        this.vaultService = vaultService;
        Text = "Change Master Password";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(540, 235);
        Padding = new Padding(16);
        FormIconService.Apply(this);
        UiTheme.Apply(this);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 175));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(layout, "Current password", currentPassword, 0);
        AddRow(layout, "New password", newPassword, 1);
        AddRow(layout, "Confirm new password", confirmation, 2);
        var help = new Label { Text = "Use at least 12 characters. The vault will be re-encrypted with Argon2id.", Dock = DockStyle.Fill };
        layout.Controls.Add(help, 0, 3);
        layout.SetColumnSpan(help, 2);
        var save = new Button { Text = "Change", Width = 100 };
        save.Click += Save_Click;
        var cancel = new Button { Text = "Cancel", Width = 100, DialogResult = DialogResult.Cancel };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(save);
        layout.Controls.Add(buttons, 0, 4);
        layout.SetColumnSpan(buttons, 2);
        Controls.Add(layout);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private void Save_Click(object? sender, EventArgs e)
    {
        if (!string.Equals(newPassword.Text, confirmation.Text, StringComparison.Ordinal))
        {
            MessageBox.Show("The new password confirmation does not match.", "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (!vaultService.TryChangeMasterPassword(currentPassword.Text, newPassword.Text, out var error))
        {
            MessageBox.Show(error, "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        DialogResult = DialogResult.OK;
        Close();
    }

    private static TextBox CreatePasswordBox() => new() { Dock = DockStyle.Fill, UseSystemPasswordChar = true, ShortcutsEnabled = false };
    private static void AddRow(TableLayoutPanel layout, string label, Control control, int row)
    {
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        layout.Controls.Add(control, 1, row);
    }
}
