namespace PasswordTool.WinForms;

using PasswordTool.Core.Models;

public sealed class UnlockVaultForm : Form
{
    private readonly bool canUseGoogleAuthenticatorLogin;
    private readonly VaultLoginMode loginMode;
    private readonly RadioButton masterPasswordOption = new();
    private readonly RadioButton googleAuthenticatorOption = new();
    private readonly TextBox masterPasswordTextBox = new();
    private readonly TextBox googleAuthenticatorCodeTextBox = new();
    private readonly CheckBox showPasswordCheckBox = new();
    private readonly Label googleAuthenticatorHintLabel = new();

    public UnlockVaultForm(bool canUseGoogleAuthenticatorLogin, VaultLoginMode loginMode)
    {
        this.canUseGoogleAuthenticatorLogin = canUseGoogleAuthenticatorLogin;
        this.loginMode = loginMode;
        BuildInterface();
        FormIconService.Apply(this);
    }

    public string MasterPassword { get; private set; } = string.Empty;

    public string GoogleAuthenticatorCode { get; private set; } = string.Empty;

    public bool UseGoogleAuthenticatorLogin { get; private set; }

    private void BuildInterface()
    {
        Text = "Unlock Vault";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(620, 255);
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

        masterPasswordOption.Text = "Master Password";
        masterPasswordOption.AutoSize = true;
        masterPasswordOption.Checked = loginMode != VaultLoginMode.GoogleAuthenticatorCode || !canUseGoogleAuthenticatorLogin;
        masterPasswordOption.Margin = new Padding(0, 5, 18, 0);
        masterPasswordOption.CheckedChanged += (_, _) => UpdateLoginOptionState();

        googleAuthenticatorOption.Text = "Google Authenticator";
        googleAuthenticatorOption.AutoSize = true;
        googleAuthenticatorOption.Enabled = canUseGoogleAuthenticatorLogin;
        googleAuthenticatorOption.Margin = new Padding(0, 5, 0, 0);
        googleAuthenticatorOption.CheckedChanged += (_, _) => UpdateLoginOptionState();
        googleAuthenticatorOption.Checked = loginMode == VaultLoginMode.GoogleAuthenticatorCode && canUseGoogleAuthenticatorLogin;

        var loginOptionPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        loginOptionPanel.Controls.Add(masterPasswordOption);
        loginOptionPanel.Controls.Add(googleAuthenticatorOption);

        masterPasswordTextBox.Dock = DockStyle.Fill;
        masterPasswordTextBox.UseSystemPasswordChar = true;

        googleAuthenticatorCodeTextBox.Dock = DockStyle.Left;
        googleAuthenticatorCodeTextBox.Width = 145;
        googleAuthenticatorCodeTextBox.MaxLength = 6;
        googleAuthenticatorCodeTextBox.TextAlign = HorizontalAlignment.Center;

        googleAuthenticatorHintLabel.Dock = DockStyle.Fill;
        googleAuthenticatorHintLabel.TextAlign = ContentAlignment.MiddleLeft;

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
        layout.Controls.Add(loginOptionPanel, 1, 0);
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
        UpdateLoginOptionState();
    }

    private void OkButton_Click(object? sender, EventArgs e)
    {
        UseGoogleAuthenticatorLogin = googleAuthenticatorOption.Checked && googleAuthenticatorOption.Enabled;

        if (UseGoogleAuthenticatorLogin)
        {
            if (string.IsNullOrWhiteSpace(googleAuthenticatorCodeTextBox.Text))
            {
                MessageBox.Show("Google Authenticator code is required.", "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            MasterPassword = string.Empty;
            GoogleAuthenticatorCode = googleAuthenticatorCodeTextBox.Text.Trim();
            DialogResult = DialogResult.OK;
            Close();
            return;
        }

        if (string.IsNullOrWhiteSpace(masterPasswordTextBox.Text))
        {
            MessageBox.Show("Master Password is required.", "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        MasterPassword = masterPasswordTextBox.Text;
        GoogleAuthenticatorCode = string.Empty;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void UpdateLoginOptionState()
    {
        var useGoogleAuthenticator = googleAuthenticatorOption.Checked && googleAuthenticatorOption.Enabled;

        masterPasswordTextBox.Enabled = !useGoogleAuthenticator;
        showPasswordCheckBox.Enabled = !useGoogleAuthenticator;
        googleAuthenticatorCodeTextBox.Enabled = useGoogleAuthenticator;

        googleAuthenticatorHintLabel.Text = canUseGoogleAuthenticatorLogin
            ? "Google Authenticator login uses the 1-day token created by a Master Password login. Master Password remains available as recovery."
            : "Google Authenticator login is available for 1 day after a successful Master Password login.";
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
