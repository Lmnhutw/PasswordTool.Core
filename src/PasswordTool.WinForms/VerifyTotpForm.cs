namespace PasswordTool.WinForms;

public sealed class VerifyTotpForm : Form
{
    private readonly Func<string, bool> verifyCode;
    private readonly TextBox codeTextBox = new();

    public VerifyTotpForm(string title, string prompt, Func<string, bool> verifyCode)
    {
        this.verifyCode = verifyCode;
        BuildInterface(title, prompt);
        FormIconService.Apply(this);
        UiTheme.Apply(this);
    }

    public string Code { get; private set; } = string.Empty;

    private void BuildInterface(string title, string prompt)
    {
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(440, 180);
        Padding = new Padding(16);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        var promptLabel = new Label
        {
            Text = prompt,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };

        codeTextBox.Dock = DockStyle.Left;
        codeTextBox.Width = 140;
        codeTextBox.MaxLength = 6;
        codeTextBox.TextAlign = HorizontalAlignment.Center;

        var okButton = new Button
        {
            Text = "Verify",
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

        layout.Controls.Add(promptLabel, 0, 0);
        layout.SetColumnSpan(promptLabel, 2);
        layout.Controls.Add(CreateLabel("Code"), 0, 1);
        layout.Controls.Add(codeTextBox, 1, 1);
        layout.Controls.Add(buttonRow, 0, 3);
        layout.SetColumnSpan(buttonRow, 2);

        Controls.Add(layout);
        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    private void OkButton_Click(object? sender, EventArgs e)
    {
        var code = codeTextBox.Text.Trim();
        if (code.Length != 6 || !code.All(char.IsDigit))
        {
            MessageBox.Show("Enter the 6-digit Google Authenticator code.", "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!verifyCode(code))
        {
            MessageBox.Show("Invalid Google Authenticator code.", "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            codeTextBox.SelectAll();
            codeTextBox.Focus();
            return;
        }

        Code = code;
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
