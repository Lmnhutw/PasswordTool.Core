using PasswordTool.Core.Models;
using PasswordTool.Core.Services;

namespace PasswordTool.WinForms;

public sealed class VaultSettingsForm : Form
{
    private readonly VaultService vaultService;
    private readonly RadioButton hybridOption = new();
    private readonly RadioButton googleAuthenticatorOption = new();
    private readonly TextBox masterPasswordTextBox = new();

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
        ClientSize = new Size(590, 320);
        Padding = new Padding(16);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 6 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
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

        layout.Controls.Add(CreateLabel("Sign-in mode"), 0, 0);
        layout.Controls.Add(modePanel, 1, 0);
        layout.SetRowSpan(modePanel, 2);
        layout.Controls.Add(CreateLabel("Master Password"), 0, 3);
        layout.Controls.Add(masterPasswordTextBox, 1, 3);
        layout.Controls.Add(helpLabel, 0, 4);
        layout.SetColumnSpan(helpLabel, 2);
        layout.Controls.Add(buttonRow, 0, 5);
        layout.SetColumnSpan(buttonRow, 2);
        Controls.Add(layout);
        AcceptButton = saveButton;
        CancelButton = cancelButton;
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
