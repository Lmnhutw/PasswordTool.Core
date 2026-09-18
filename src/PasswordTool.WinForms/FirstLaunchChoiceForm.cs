namespace PasswordTool.WinForms;

public sealed class FirstLaunchChoiceForm : Form
{
    public FirstLaunchChoiceForm()
    {
        Text = "Welcome to PasswordTool";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(560, 235);
        Padding = new Padding(20);
        FormIconService.Apply(this);
        UiTheme.Apply(this);

        var explanation = new Label
        {
            Dock = DockStyle.Top,
            Height = 92,
            Text = "Create an empty vault, or recover vault items from an encrypted PasswordTool backup. " +
                "Recovery uses the backup passphrase, then creates a new Master Password and a new PasswordTool Authenticator for this installation.",
            TextAlign = ContentAlignment.MiddleLeft
        };
        var createButton = new Button { Text = "Create a new empty vault", Width = 220, Height = 42 };
        createButton.Click += (_, _) => Complete(recover: false);
        var recoverButton = new Button { Text = "Recover from encrypted backup", Width = 240, Height = 42 };
        recoverButton.Click += (_, _) => Complete(recover: true);
        var cancelButton = new Button { Text = "Cancel", Width = 100, Height = 34, DialogResult = DialogResult.Cancel };
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 58,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        actions.Controls.Add(createButton);
        actions.Controls.Add(recoverButton);
        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 40,
            FlowDirection = FlowDirection.RightToLeft
        };
        footer.Controls.Add(cancelButton);
        Controls.Add(footer);
        Controls.Add(actions);
        Controls.Add(explanation);
        CancelButton = cancelButton;
    }

    public bool RecoverFromBackup { get; private set; }

    private void Complete(bool recover)
    {
        RecoverFromBackup = recover;
        DialogResult = DialogResult.OK;
        Close();
    }
}
