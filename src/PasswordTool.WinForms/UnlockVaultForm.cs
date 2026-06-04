namespace PasswordTool.WinForms;

public sealed class UnlockVaultForm : Form
{
    private readonly TextBox masterPasswordTextBox = new();
    private readonly CheckBox showPasswordCheckBox = new();

    public UnlockVaultForm()
    {
        BuildInterface();
        FormIconService.Apply(this);
    }

    public string MasterPassword { get; private set; } = string.Empty;

    private void BuildInterface()
    {
        Text = "Unlock Vault";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(460, 170);
        Padding = new Padding(16);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        masterPasswordTextBox.Dock = DockStyle.Fill;
        masterPasswordTextBox.UseSystemPasswordChar = true;

        showPasswordCheckBox.Text = "Show password";
        showPasswordCheckBox.Dock = DockStyle.Left;
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

        layout.Controls.Add(CreateLabel("Master Password"), 0, 0);
        layout.Controls.Add(masterPasswordTextBox, 1, 0);
        layout.Controls.Add(new Panel(), 0, 1);
        layout.Controls.Add(showPasswordCheckBox, 1, 1);
        layout.Controls.Add(buttonRow, 0, 3);
        layout.SetColumnSpan(buttonRow, 2);

        Controls.Add(layout);
        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    private void OkButton_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(masterPasswordTextBox.Text))
        {
            MessageBox.Show("Enter your Master Password.", "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        MasterPassword = masterPasswordTextBox.Text;
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
