namespace PasswordTool.WinForms;

public sealed class UnlockVaultForm : Form
{
    private readonly TextBox masterPasswordTextBox = new();
    private readonly TextBox googleAuthenticatorCodeTextBox = new();
    private readonly CheckBox showPasswordCheckBox = new();

    public UnlockVaultForm()
    {
        BuildInterface();
        FormIconService.Apply(this);
    }

    public string MasterPassword { get; private set; } = string.Empty;

    public string GoogleAuthenticatorCode { get; private set; } = string.Empty;

    private void BuildInterface()
    {
        Text = "Unlock Vault";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(540, 245);
        Padding = new Padding(16);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 7
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 175));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        var loginOptionLabel = new Label
        {
            Text = "Master Password + Google Authenticator",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };

        masterPasswordTextBox.Dock = DockStyle.Fill;
        masterPasswordTextBox.UseSystemPasswordChar = true;

        googleAuthenticatorCodeTextBox.Dock = DockStyle.Left;
        googleAuthenticatorCodeTextBox.Width = 145;
        googleAuthenticatorCodeTextBox.MaxLength = 6;
        googleAuthenticatorCodeTextBox.TextAlign = HorizontalAlignment.Center;

        var googleAuthenticatorHintLabel = new Label
        {
            Text = "Required for vaults paired with Google Authenticator.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };

        showPasswordCheckBox.Text = "Show";
        showPasswordCheckBox.AutoSize = true;
        showPasswordCheckBox.Anchor = AnchorStyles.Left;
        showPasswordCheckBox.Margin = new Padding(0, 4, 0, 0);
        showPasswordCheckBox.CheckedChanged += (_, _) => masterPasswordTextBox.UseSystemPasswordChar = !showPasswordCheckBox.Checked;

        var okButton = new Button
        {
            Text = "Continue",
            Width = 110,
            DialogResult = DialogResult.None
        };
        okButton.Click += OkButton_Click;

        var cancelButton = new Button
        {
            Text = "Cancel",
            Width = 110,
            DialogResult = DialogResult.Cancel
        };

        var buttonRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft
        };
        buttonRow.Controls.Add(cancelButton);
        buttonRow.Controls.Add(okButton);

        layout.Controls.Add(CreateLabel("Login option"), 0, 0);
        layout.Controls.Add(loginOptionLabel, 1, 0);
        layout.Controls.Add(CreateLabel("Master Password"), 0, 1);
        layout.Controls.Add(masterPasswordTextBox, 1, 1);
        layout.Controls.Add(new Panel(), 0, 2);
        layout.Controls.Add(showPasswordCheckBox, 1, 2);
        layout.Controls.Add(CreateLabel("Google Authenticator"), 0, 3);
        layout.Controls.Add(googleAuthenticatorCodeTextBox, 1, 3);
        layout.Controls.Add(new Panel(), 0, 4);
        layout.Controls.Add(googleAuthenticatorHintLabel, 1, 4);
        layout.Controls.Add(buttonRow, 0, 6);
        layout.SetColumnSpan(buttonRow, 2);

        Controls.Add(layout);
        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    private void OkButton_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(masterPasswordTextBox.Text))
        {
            MessageBox.Show("Master Password is required.", "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        MasterPassword = masterPasswordTextBox.Text;
        GoogleAuthenticatorCode = googleAuthenticatorCodeTextBox.Text.Trim();
        DialogResult = DialogResult.OK;
        Close();
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
}
