using PasswordTool.Core.Models;
using PasswordTool.Core.Services;

namespace PasswordTool.WinForms;

internal sealed class PasswordGeneratorForm : Form
{
    private readonly PasswordGeneratorService generator = new();
    private readonly RadioButton passwordMode = new() { Text = "Password", Checked = true, AutoSize = true };
    private readonly RadioButton passphraseMode = new() { Text = "Passphrase", AutoSize = true };
    private readonly NumericUpDown lengthInput = new() { Minimum = 8, Maximum = 128, Value = 20, Width = 90 };
    private readonly NumericUpDown chunksInput = new() { Minimum = 4, Maximum = 12, Value = 7, Width = 90 };
    private readonly CheckBox uppercase = new() { Text = "Uppercase", Checked = true, AutoSize = true };
    private readonly CheckBox lowercase = new() { Text = "Lowercase", Checked = true, AutoSize = true };
    private readonly CheckBox digits = new() { Text = "Digits", Checked = true, AutoSize = true };
    private readonly CheckBox symbols = new() { Text = "Symbols", Checked = true, AutoSize = true };
    private readonly TextBox generatedTextBox = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly Label strengthLabel = new() { AutoSize = true };
    private readonly Label sizeLabel = new() { Text = "Length", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft };

    public PasswordGeneratorForm()
    {
        Text = "Generate Password";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(660, 320);
        Padding = new Padding(16);
        FormIconService.Apply(this);
        BuildInterface();
        Generate();
    }

    public string GeneratedValue => generatedTextBox.Text;

    private void BuildInterface()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var modes = new FlowLayoutPanel { Dock = DockStyle.Fill };
        modes.Controls.Add(passwordMode);
        modes.Controls.Add(passphraseMode);
        passwordMode.CheckedChanged += (_, _) => UpdateMode();

        var sizeRow = new FlowLayoutPanel { Dock = DockStyle.Fill };
        sizeRow.Controls.Add(sizeLabel);
        sizeRow.Controls.Add(lengthInput);
        sizeRow.Controls.Add(chunksInput);

        var groups = new FlowLayoutPanel { Dock = DockStyle.Fill };
        groups.Controls.Add(uppercase);
        groups.Controls.Add(lowercase);
        groups.Controls.Add(digits);
        groups.Controls.Add(symbols);

        var generateButton = new Button { Text = "Generate", Width = 110 };
        generateButton.Click += (_, _) => Generate();
        var generatedRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        generatedRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        generatedRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        generatedRow.Controls.Add(generatedTextBox, 0, 0);
        generatedRow.Controls.Add(generateButton, 1, 0);

        var useButton = new Button { Text = "Use This", Width = 100, DialogResult = DialogResult.OK };
        var cancelButton = new Button { Text = "Cancel", Width = 100, DialogResult = DialogResult.Cancel };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(useButton);

        root.Controls.Add(modes, 0, 0);
        root.Controls.Add(sizeRow, 0, 1);
        root.Controls.Add(groups, 0, 2);
        root.Controls.Add(generatedRow, 0, 3);
        root.Controls.Add(strengthLabel, 0, 4);
        root.Controls.Add(buttons, 0, 5);
        Controls.Add(root);
        AcceptButton = useButton;
        CancelButton = cancelButton;
        UpdateMode();
    }

    private void UpdateMode()
    {
        var isPassword = passwordMode.Checked;
        sizeLabel.Text = isPassword ? "Length" : "Chunks";
        lengthInput.Visible = isPassword;
        chunksInput.Visible = !isPassword;
        uppercase.Visible = isPassword;
        lowercase.Visible = isPassword;
        digits.Visible = isPassword;
        symbols.Visible = isPassword;
        if (Visible) Generate();
    }

    private void Generate()
    {
        try
        {
            PasswordStrength strength;
            if (passwordMode.Checked)
            {
                generatedTextBox.Text = generator.GeneratePassword(new PasswordGenerationOptions
                {
                    Length = (int)lengthInput.Value,
                    IncludeUppercase = uppercase.Checked,
                    IncludeLowercase = lowercase.Checked,
                    IncludeDigits = digits.Checked,
                    IncludeSymbols = symbols.Checked
                });
                strength = generator.EstimatePasswordStrength(generatedTextBox.Text);
            }
            else
            {
                generatedTextBox.Text = generator.GeneratePassphrase((int)chunksInput.Value);
                strength = generator.EstimatePassphraseStrength((int)chunksInput.Value);
            }
            strengthLabel.Text = $"Estimated strength: {strength.Rating} ({strength.EstimatedEntropyBits:F0} bits)";
        }
        catch (ArgumentException ex)
        {
            MessageBox.Show(ex.Message, "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
