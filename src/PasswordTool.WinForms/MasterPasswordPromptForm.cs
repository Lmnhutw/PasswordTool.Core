namespace PasswordTool.WinForms;

public sealed class MasterPasswordPromptForm : Form
{
    private readonly TextBox passwordTextBox = new();

    public MasterPasswordPromptForm(string title, string message)
    {
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(500, 150);
        Padding = new Padding(16);
        FormIconService.Apply(this);

        var messageLabel = new Label { Text = message, Dock = DockStyle.Top, Height = 42 };
        passwordTextBox.Dock = DockStyle.Top;
        passwordTextBox.UseSystemPasswordChar = true;
        passwordTextBox.ShortcutsEnabled = false;
        var ok = new Button { Text = "Confirm", Width = 100, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", Width = 100, DialogResult = DialogResult.Cancel };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        Controls.Add(buttons);
        Controls.Add(passwordTextBox);
        Controls.Add(messageLabel);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    public string MasterPassword => passwordTextBox.Text;
}
