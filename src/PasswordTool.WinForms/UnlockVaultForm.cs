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
    private readonly Label googleAuthenticatorFieldLabel = new();

    public UnlockVaultForm(bool canUseGoogleAuthenticatorLogin, VaultLoginMode loginMode)
    {
        this.canUseGoogleAuthenticatorLogin = canUseGoogleAuthenticatorLogin;
        this.loginMode = loginMode;
        BuildInterface();
        FormIconService.Apply(this);
        UiTheme.Apply(this);
        UpdateLoginOptionState();
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
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(660, 360);
        Padding = new Padding(24, 20, 24, 20);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            BackColor = UiTheme.WindowBackground
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

        var heading = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty
        };
        heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        heading.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        heading.Controls.Add(new Label
        {
            Text = "Unlock your vault",
            Dock = DockStyle.Fill,
            Font = new Font(UiTheme.DefaultFont.FontFamily, 15F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        heading.Controls.Add(new Label
        {
            Text = "Choose an available sign-in method.",
            Dock = DockStyle.Fill,
            ForeColor = UiTheme.TextSecondary,
            TextAlign = ContentAlignment.TopLeft
        }, 0, 1);

        masterPasswordOption.Text = "Master Password";
        masterPasswordOption.AutoSize = true;
        masterPasswordOption.Checked = loginMode != VaultLoginMode.GoogleAuthenticatorCode || !canUseGoogleAuthenticatorLogin;
        masterPasswordOption.Margin = new Padding(0, 7, 24, 0);
        masterPasswordOption.CheckedChanged += (_, _) => UpdateLoginOptionState();

        googleAuthenticatorOption.Text = "Google Authenticator";
        googleAuthenticatorOption.AutoSize = true;
        googleAuthenticatorOption.Enabled = canUseGoogleAuthenticatorLogin;
        googleAuthenticatorOption.Margin = new Padding(0, 7, 0, 0);
        googleAuthenticatorOption.AccessibleDescription = "Available for one day after a successful Master Password sign-in.";
        googleAuthenticatorOption.CheckedChanged += (_, _) => UpdateLoginOptionState();
        googleAuthenticatorOption.Checked = loginMode == VaultLoginMode.GoogleAuthenticatorCode && canUseGoogleAuthenticatorLogin;

        var loginOptionPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = Padding.Empty
        };
        loginOptionPanel.Controls.Add(masterPasswordOption);
        loginOptionPanel.Controls.Add(googleAuthenticatorOption);

        masterPasswordTextBox.Dock = DockStyle.Fill;
        masterPasswordTextBox.UseSystemPasswordChar = true;
        masterPasswordTextBox.AccessibleName = "Master Password";

        var masterPasswordSection = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 2,
            Margin = Padding.Empty
        };
        masterPasswordSection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        masterPasswordSection.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 12));
        masterPasswordSection.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 56));
        masterPasswordSection.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        masterPasswordSection.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        var masterPasswordLabel = CreateLabel("Master Password");
        masterPasswordSection.Controls.Add(masterPasswordLabel, 0, 0);
        masterPasswordSection.SetColumnSpan(masterPasswordLabel, 3);
        masterPasswordSection.Controls.Add(masterPasswordTextBox, 0, 1);

        googleAuthenticatorCodeTextBox.Dock = DockStyle.Fill;
        googleAuthenticatorCodeTextBox.MaxLength = 6;
        googleAuthenticatorCodeTextBox.TextAlign = HorizontalAlignment.Center;
        googleAuthenticatorCodeTextBox.AccessibleName = "Google Authenticator code";

        googleAuthenticatorHintLabel.Dock = DockStyle.Fill;
        googleAuthenticatorHintLabel.TextAlign = ContentAlignment.TopLeft;
        googleAuthenticatorHintLabel.AutoEllipsis = false;

        googleAuthenticatorFieldLabel.Text = "Google Authenticator code";
        googleAuthenticatorFieldLabel.Dock = DockStyle.Fill;
        googleAuthenticatorFieldLabel.TextAlign = ContentAlignment.MiddleLeft;

        var authenticatorSection = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(0, 4, 0, 0)
        };
        authenticatorSection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        authenticatorSection.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        authenticatorSection.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        authenticatorSection.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        googleAuthenticatorCodeTextBox.Dock = DockStyle.None;
        googleAuthenticatorCodeTextBox.Anchor = AnchorStyles.Left;
        googleAuthenticatorCodeTextBox.AutoSize = false;
        googleAuthenticatorCodeTextBox.Width = 220;
        googleAuthenticatorCodeTextBox.Height = 30;
        googleAuthenticatorCodeTextBox.Margin = new Padding(0, 2, 0, 4);
        authenticatorSection.Controls.Add(googleAuthenticatorFieldLabel, 0, 0);
        authenticatorSection.Controls.Add(googleAuthenticatorCodeTextBox, 0, 1);
        authenticatorSection.Controls.Add(googleAuthenticatorHintLabel, 0, 2);

        showPasswordCheckBox.Text = "Show";
        showPasswordCheckBox.AutoSize = true;
        showPasswordCheckBox.Anchor = AnchorStyles.Left;
        showPasswordCheckBox.Margin = new Padding(0, 4, 0, 0);
        showPasswordCheckBox.CheckedChanged += (_, _) => masterPasswordTextBox.UseSystemPasswordChar = !showPasswordCheckBox.Checked;
        masterPasswordSection.Controls.Add(showPasswordCheckBox, 2, 1);

        var okButton = new Button
        {
            Text = "Continue",
            Width = 110,
            Height = 38,
            Margin = Padding.Empty,
            DialogResult = DialogResult.None
        };
        okButton.Click += OkButton_Click;

        var cancelButton = new Button
        {
            Text = "Cancel",
            Width = 110,
            Height = 38,
            Margin = new Padding(8, 0, 0, 0),
            DialogResult = DialogResult.Cancel
        };

        var buttonRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 4, 0, 0),
            Margin = Padding.Empty
        };
        buttonRow.Controls.Add(cancelButton);
        buttonRow.Controls.Add(okButton);

        layout.Controls.Add(heading, 0, 0);
        layout.Controls.Add(loginOptionPanel, 0, 1);
        layout.Controls.Add(masterPasswordSection, 0, 2);
        layout.Controls.Add(authenticatorSection, 0, 3);
        layout.Controls.Add(buttonRow, 0, 5);

        Controls.Add(layout);
        AcceptButton = okButton;
        CancelButton = cancelButton;
        UpdateLoginOptionState();
        Shown += (_, _) =>
        {
            if (googleAuthenticatorOption.Checked && googleAuthenticatorOption.Enabled)
                googleAuthenticatorCodeTextBox.Focus();
            else
                masterPasswordTextBox.Focus();
        };
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

        if (!canUseGoogleAuthenticatorLogin && !masterPasswordOption.Checked)
        {
            masterPasswordOption.Checked = true;
            useGoogleAuthenticator = false;
        }

        masterPasswordTextBox.Enabled = !useGoogleAuthenticator;
        showPasswordCheckBox.Enabled = !useGoogleAuthenticator;
        googleAuthenticatorCodeTextBox.Enabled = canUseGoogleAuthenticatorLogin && useGoogleAuthenticator;
        googleAuthenticatorCodeTextBox.TabStop = googleAuthenticatorCodeTextBox.Enabled;
        googleAuthenticatorCodeTextBox.BackColor = googleAuthenticatorCodeTextBox.Enabled ? UiTheme.Surface : SystemColors.Control;
        googleAuthenticatorFieldLabel.ForeColor = canUseGoogleAuthenticatorLogin ? UiTheme.TextPrimary : SystemColors.GrayText;

        googleAuthenticatorHintLabel.Text = canUseGoogleAuthenticatorLogin
            ? "Use the 6-digit code. This option remains available for 1 day after a successful Master Password sign-in."
            : "Unavailable right now. Sign in with your Master Password to enable this option for 1 day.";
        googleAuthenticatorHintLabel.ForeColor = canUseGoogleAuthenticatorLogin ? UiTheme.TextSecondary : SystemColors.GrayText;

        if (useGoogleAuthenticator) googleAuthenticatorCodeTextBox.Focus();
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
