using PasswordTool.Core.Models;
using PasswordTool.Core.Services;

namespace PasswordTool.WinForms;

public sealed class VaultSettingsForm : Form
{
    private readonly VaultService vaultService;
    private readonly RadioButton hybridOption = new();
    private readonly RadioButton googleAuthenticatorOption = new();
    private readonly TextBox masterPasswordTextBox = new();
    private readonly TotpService totpService = new();

    public bool RequiresVaultLock { get; private set; }

    public VaultSettingsForm(VaultService vaultService)
    {
        this.vaultService = vaultService;
        BuildInterface();
        FormIconService.Apply(this);
    }

    private void BuildInterface()
    {
        Text = "Settings";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(720, 455);
        Padding = new Padding(16);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 8 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 12));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        hybridOption.Text = "Hybrid login (default)";
        hybridOption.AutoSize = true;
        hybridOption.Checked = vaultService.LoginMode == VaultLoginMode.Hybrid;

        googleAuthenticatorOption.Text = "Google Authenticator code";
        googleAuthenticatorOption.AutoSize = true;
        googleAuthenticatorOption.Checked = vaultService.LoginMode == VaultLoginMode.GoogleAuthenticatorCode;
        googleAuthenticatorOption.Enabled = vaultService.CanUnlockWithGoogleAuthenticatorToken;

        var modePanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
        modePanel.Controls.Add(hybridOption);
        modePanel.Controls.Add(googleAuthenticatorOption);

        masterPasswordTextBox.Dock = DockStyle.Fill;
        masterPasswordTextBox.UseSystemPasswordChar = true;
        masterPasswordTextBox.ShortcutsEnabled = false;

        var helpLabel = new Label
        {
            Text = googleAuthenticatorOption.Enabled
                ? "Changing sign-in mode requires your Master Password. Google Authenticator sign-in needs its current 1-day trusted token; Master Password stays available if that token expires."
                : "Google Authenticator code can be selected after a successful Master Password sign-in creates its 1-day trusted token.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var saveButton = new Button { Text = "Save", Width = 100 };
        saveButton.Click += SaveButton_Click;
        var cancelButton = new Button { Text = "Cancel", Width = 100, DialogResult = DialogResult.Cancel };
        var buttonRow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        buttonRow.Controls.Add(cancelButton);
        buttonRow.Controls.Add(saveButton);

        var securityActions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true };
        var changeMasterButton = new Button { Text = "Change Master Password", Width = 170, Height = 32 };
        changeMasterButton.Click += (_, _) => ChangeMasterPassword();
        var resetAuthenticatorButton = new Button { Text = "Reset Authenticator", Width = 155, Height = 32 };
        resetAuthenticatorButton.Click += (_, _) => ResetAuthenticator();
        var snapshotsButton = new Button { Text = "Snapshots", Width = 110, Height = 32 };
        snapshotsButton.Click += (_, _) => OpenSnapshots();
        var upgradeKdfButton = new Button { Text = "Upgrade KDF", Width = 115, Height = 32, Enabled = vaultService.NeedsKdfUpgrade };
        upgradeKdfButton.Click += (_, _) => UpgradeKdf(upgradeKdfButton);
        securityActions.Controls.AddRange([changeMasterButton, resetAuthenticatorButton, snapshotsButton, upgradeKdfButton]);

        layout.Controls.Add(CreateLabel("Sign-in mode"), 0, 0);
        layout.Controls.Add(modePanel, 1, 0);
        layout.SetRowSpan(modePanel, 2);
        layout.Controls.Add(CreateLabel("Master Password"), 0, 3);
        layout.Controls.Add(masterPasswordTextBox, 1, 3);
        layout.Controls.Add(helpLabel, 0, 4);
        layout.SetColumnSpan(helpLabel, 2);
        layout.Controls.Add(securityActions, 0, 5);
        layout.SetColumnSpan(securityActions, 2);
        layout.Controls.Add(buttonRow, 0, 7);
        layout.SetColumnSpan(buttonRow, 2);
        Controls.Add(layout);
        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    private void ChangeMasterPassword()
    {
        using var form = new ChangeMasterPasswordForm(vaultService);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            masterPasswordTextBox.Clear();
            MessageBox.Show("The Master Password was changed and the vault was re-encrypted.", "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void ResetAuthenticator()
    {
        using var masterPrompt = new MasterPasswordPromptForm("Reset Authenticator", "Enter the Master Password before replacing the PasswordTool Authenticator secret.");
        if (masterPrompt.ShowDialog(this) != DialogResult.OK) return;
        var secret = totpService.GenerateSecret();
        var uri = totpService.CreateOtpAuthUri(secret, "PasswordTool", Environment.UserName);
        using var setup = new SetupAuthenticatorForm(secret, uri, totpService);
        if (setup.ShowDialog(this) != DialogResult.OK) return;
        if (!vaultService.TryResetAuthenticator(masterPrompt.MasterPassword, secret, setup.VerifiedCode, out var error))
        {
            MessageBox.Show(error, "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        MessageBox.Show("Authenticator reset complete. The previous secret and trusted token no longer work.", "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void OpenSnapshots()
    {
        using var form = new VaultSnapshotsForm(vaultService);
        form.ShowDialog(this);
        if (!form.Restored) return;
        RequiresVaultLock = true;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void UpgradeKdf(Button button)
    {
        using var prompt = new MasterPasswordPromptForm("Upgrade KDF", "Enter the Master Password to re-encrypt this vault with the current Argon2id settings.");
        if (prompt.ShowDialog(this) != DialogResult.OK) return;
        if (!vaultService.TryUpgradeKdf(prompt.MasterPassword, out var error))
        {
            MessageBox.Show(error, "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        button.Enabled = false;
        MessageBox.Show("The vault now uses the current Argon2id settings.", "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        var selectedMode = googleAuthenticatorOption.Checked
            ? VaultLoginMode.GoogleAuthenticatorCode
            : VaultLoginMode.Hybrid;

        if (selectedMode == VaultLoginMode.GoogleAuthenticatorCode && !googleAuthenticatorOption.Enabled)
        {
            MessageBox.Show("Google Authenticator sign-in is not available yet.", "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!vaultService.TrySetLoginMode(masterPasswordTextBox.Text, selectedMode, out var errorMessage))
        {
            MessageBox.Show(errorMessage, "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Error);
            masterPasswordTextBox.SelectAll();
            masterPasswordTextBox.Focus();
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private static Label CreateLabel(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft
    };
}
