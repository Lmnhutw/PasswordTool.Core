namespace PasswordTool.WinForms;

internal sealed class BackupPassphraseForm : Form
{
    private readonly bool requireConfirmation;
    private readonly TextBox passphraseTextBox = new();
    private readonly TextBox confirmationTextBox = new();

    public BackupPassphraseForm(bool requireConfirmation)
    {
        this.requireConfirmation = requireConfirmation;
        Text = requireConfirmation ? "Protect JSON Backup" : "Open JSON Backup";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(540, requireConfirmation ? 250 : 210);
        Padding = new Padding(16);
        FormIconService.Apply(this);
        BuildInterface();
    }

    public string Passphrase { get; private set; } = string.Empty;

    private void BuildInterface()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = requireConfirmation ? 4 : 3 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 75));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        if (requireConfirmation)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        }
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

        var explanation = new Label
        {
            Text = requireConfirmation
                ? "Create a separate password (at least 12 characters) to encrypt this exported backup. You will need this exact password to import it later; it is different from your Master Password."
                : "Enter the backup password you created when this file was exported. It decrypts this backup and is different from your Master Password.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        layout.Controls.Add(explanation, 0, 0);
        layout.SetColumnSpan(explanation, 2);

        passphraseTextBox.Dock = DockStyle.Fill;
        passphraseTextBox.UseSystemPasswordChar = true;
        layout.Controls.Add(CreateLabel(requireConfirmation ? "Export password" : "Backup password"), 0, 1);
        layout.Controls.Add(passphraseTextBox, 1, 1);

        if (requireConfirmation)
        {
            confirmationTextBox.Dock = DockStyle.Fill;
            confirmationTextBox.UseSystemPasswordChar = true;
            layout.Controls.Add(CreateLabel("Confirm password"), 0, 2);
            layout.Controls.Add(confirmationTextBox, 1, 2);
        }

        var continueButton = new Button { Text = "Continue", Width = 100, DialogResult = DialogResult.None };
        continueButton.Click += ContinueButton_Click;
        var cancelButton = new Button { Text = "Cancel", Width = 100, DialogResult = DialogResult.Cancel };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(continueButton);
        var buttonRow = requireConfirmation ? 3 : 2;
        layout.Controls.Add(buttons, 0, buttonRow);
        layout.SetColumnSpan(buttons, 2);

        Controls.Add(layout);
        AcceptButton = continueButton;
        CancelButton = cancelButton;
    }

    private void ContinueButton_Click(object? sender, EventArgs e)
    {
        if (passphraseTextBox.Text.Length < 12)
        {
            MessageBox.Show("Use a backup passphrase with at least 12 characters.", "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (requireConfirmation && passphraseTextBox.Text != confirmationTextBox.Text)
        {
            MessageBox.Show("The backup passphrases do not match.", "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Passphrase = passphraseTextBox.Text;
        DialogResult = DialogResult.OK;
        Close();
    }

    private static Label CreateLabel(string text)
    {
        return new Label { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    }
}
