using PasswordTool.Core.Services;

namespace PasswordTool.WinForms;

public sealed class CreateMasterPasswordForm : Form
{
    private readonly TextBox masterPasswordTextBox = new();
    private readonly TextBox confirmPasswordTextBox = new();
    private readonly CheckBox showPasswordCheckBox = new();

    public CreateMasterPasswordForm()
    {
        BuildInterface();
        FormIconService.Apply(this);
        UiTheme.Apply(this);
    }

    public string MasterPassword { get; private set; } = string.Empty;

    private void BuildInterface()
    {
        Text = "Create Master Password";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(520, 220);
        Padding = new Padding(16);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        masterPasswordTextBox.Dock = DockStyle.Fill;
        masterPasswordTextBox.UseSystemPasswordChar = true;

        confirmPasswordTextBox.Dock = DockStyle.Fill;
        confirmPasswordTextBox.UseSystemPasswordChar = true;

        showPasswordCheckBox.Text = "Show";
        showPasswordCheckBox.AutoSize = true;
        showPasswordCheckBox.Anchor = AnchorStyles.Left;
        showPasswordCheckBox.Margin = new Padding(0, 4, 0, 0);
        showPasswordCheckBox.CheckedChanged += (_, _) =>
        {
            masterPasswordTextBox.UseSystemPasswordChar = !showPasswordCheckBox.Checked;
            confirmPasswordTextBox.UseSystemPasswordChar = !showPasswordCheckBox.Checked;
        };

        var guidanceLabel = new Label
        {
            Text = "Use at least 12 characters. This password is never stored and cannot be recovered.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var okButton = new Button
        {
            Text = "Create",
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
        layout.Controls.Add(CreateLabel("Confirm"), 0, 1);
        layout.Controls.Add(confirmPasswordTextBox, 1, 1);
        layout.Controls.Add(new Panel(), 0, 2);
        layout.Controls.Add(showPasswordCheckBox, 1, 2);
        layout.Controls.Add(new Panel(), 0, 3);
        layout.Controls.Add(guidanceLabel, 1, 3);
        layout.Controls.Add(buttonRow, 0, 4);
        layout.SetColumnSpan(buttonRow, 2);

        Controls.Add(layout);
        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    private void OkButton_Click(object? sender, EventArgs e)
    {
        try
        {
            MasterPasswordService.ValidateNewMasterPassword(masterPasswordTextBox.Text);

            if (!string.Equals(masterPasswordTextBox.Text, confirmPasswordTextBox.Text, StringComparison.Ordinal))
            {
                ShowWarning("The Master Password values do not match.");
                return;
            }

            MasterPassword = masterPasswordTextBox.Text;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (ArgumentException ex)
        {
            ShowWarning(ex.Message);
        }
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

    private static void ShowWarning(string message)
    {
        MessageBox.Show(message, "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
