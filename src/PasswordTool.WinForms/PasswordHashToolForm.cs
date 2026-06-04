using PasswordTool.Core.Abstractions;
using PasswordTool.Core.Hashers;
using PasswordTool.Core.Inspection;
using PasswordTool.Core.Models;
using PasswordTool.Core.Registry;
using PasswordTool.Core.Services;

namespace PasswordTool.WinForms;

public sealed class PasswordHashToolForm : Form
{
    private readonly PasswordHasherRegistry registry = new();
    private readonly PasswordHasherFactory hasherFactory = new();
    private readonly PasswordHashInspector hashInspector = new();

    private readonly TextBox passwordInput = new();
    private readonly CheckBox showPasswordCheckBox = new();
    private readonly ComboBox algorithmComboBox = new();
    private readonly Label algorithmDescriptionLabel = new();
    private readonly TextBox generatedHashTextBox = new();
    private readonly TextBox generatedSaltBase64TextBox = new();
    private readonly TextBox generatedHashBase64TextBox = new();
    private readonly Label base64StorageGuidanceLabel = new();
    private readonly Button generateHashButton = new();
    private readonly Button copyHashButton = new();

    private readonly TextBox verifyPasswordInput = new();
    private readonly CheckBox showVerifyPasswordCheckBox = new();
    private readonly TextBox storedHashInput = new();
    private readonly Button verifyPasswordButton = new();
    private readonly Label verificationResultLabel = new();

    private readonly Button inspectHashButton = new();
    private readonly DataGridView inspectionGrid = new();

    public PasswordHashToolForm()
    {
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1000, 820);
        MinimumSize = new Size(900, 720);
        BuildInterface();
        LoadAlgorithmOptions();
        FormIconService.Apply(this);
    }

    private void BuildInterface()
    {
        Controls.Clear();
        Text = "Password Hashing Learning Tool";
        StartPosition = FormStartPosition.CenterParent;
        Padding = new Padding(16);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 345));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 235));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.Controls.Add(CreateGenerationSection(), 0, 0);
        root.Controls.Add(CreateVerificationSection(), 0, 1);
        root.Controls.Add(CreateInspectorSection(), 0, 2);

        Controls.Add(root);
    }

    private GroupBox CreateGenerationSection()
    {
        var group = CreateGroupBox("Password generation");

        var layout = CreateSectionGrid();
        layout.RowCount = 8;
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

        passwordInput.Dock = DockStyle.Fill;
        passwordInput.UseSystemPasswordChar = true;

        showPasswordCheckBox.Text = "Show";
        showPasswordCheckBox.Dock = DockStyle.Left;
        showPasswordCheckBox.CheckedChanged += (_, _) => passwordInput.UseSystemPasswordChar = !showPasswordCheckBox.Checked;

        var passwordRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        passwordRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        passwordRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        passwordRow.Controls.Add(passwordInput, 0, 0);
        passwordRow.Controls.Add(showPasswordCheckBox, 1, 0);

        algorithmComboBox.Dock = DockStyle.Left;
        algorithmComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        algorithmComboBox.Width = 340;
        algorithmComboBox.SelectedIndexChanged += (_, _) => UpdateAlgorithmDescription();

        algorithmDescriptionLabel.Dock = DockStyle.Fill;
        algorithmDescriptionLabel.AutoEllipsis = true;
        algorithmDescriptionLabel.TextAlign = ContentAlignment.MiddleLeft;

        var algorithmRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        algorithmRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360));
        algorithmRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        algorithmRow.Controls.Add(algorithmComboBox, 0, 0);
        algorithmRow.Controls.Add(algorithmDescriptionLabel, 1, 0);

        generateHashButton.Text = "Generate Hash";
        generateHashButton.Width = 150;
        generateHashButton.Click += GenerateHashButton_Click;

        generatedHashTextBox.Dock = DockStyle.Fill;
        generatedHashTextBox.Multiline = true;
        generatedHashTextBox.ScrollBars = ScrollBars.Vertical;
        generatedHashTextBox.ReadOnly = true;

        generatedSaltBase64TextBox.Dock = DockStyle.Fill;
        generatedSaltBase64TextBox.ReadOnly = true;

        generatedHashBase64TextBox.Dock = DockStyle.Fill;
        generatedHashBase64TextBox.ReadOnly = true;

        base64StorageGuidanceLabel.Dock = DockStyle.Fill;
        base64StorageGuidanceLabel.Text = "Store the complete generated hash string in one PasswordHash text column. Salt and hash are encoded as Base64 so they can be stored as text. Base64 is not encryption.";
        base64StorageGuidanceLabel.TextAlign = ContentAlignment.MiddleLeft;

        copyHashButton.Text = "Copy Hash";
        copyHashButton.Width = 120;
        copyHashButton.Click += CopyHashButton_Click;

        layout.Controls.Add(CreateLabel("Password"), 0, 0);
        layout.Controls.Add(passwordRow, 1, 0);
        layout.Controls.Add(CreateLabel("Algorithm"), 0, 1);
        layout.Controls.Add(algorithmRow, 1, 1);
        layout.Controls.Add(new Panel { Dock = DockStyle.Fill }, 0, 2);
        layout.Controls.Add(generateHashButton, 1, 2);
        layout.Controls.Add(CreateLabel("Generated hash"), 0, 3);
        layout.Controls.Add(generatedHashTextBox, 1, 3);
        layout.Controls.Add(CreateLabel("Salt (Base64)"), 0, 4);
        layout.Controls.Add(generatedSaltBase64TextBox, 1, 4);
        layout.Controls.Add(CreateLabel("Hash output (Base64)"), 0, 5);
        layout.Controls.Add(generatedHashBase64TextBox, 1, 5);
        layout.Controls.Add(new Panel { Dock = DockStyle.Fill }, 0, 6);
        layout.Controls.Add(base64StorageGuidanceLabel, 1, 6);
        layout.Controls.Add(new Panel { Dock = DockStyle.Fill }, 0, 7);
        layout.Controls.Add(copyHashButton, 1, 7);

        group.Controls.Add(layout);
        return group;
    }

    private GroupBox CreateVerificationSection()
    {
        var group = CreateGroupBox("Password verification");

        var layout = CreateSectionGrid();
        layout.RowCount = 4;
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));

        verifyPasswordInput.Dock = DockStyle.Fill;
        verifyPasswordInput.UseSystemPasswordChar = true;

        showVerifyPasswordCheckBox.Text = "Show";
        showVerifyPasswordCheckBox.Dock = DockStyle.Left;
        showVerifyPasswordCheckBox.CheckedChanged += (_, _) => verifyPasswordInput.UseSystemPasswordChar = !showVerifyPasswordCheckBox.Checked;

        var passwordRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        passwordRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        passwordRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        passwordRow.Controls.Add(verifyPasswordInput, 0, 0);
        passwordRow.Controls.Add(showVerifyPasswordCheckBox, 1, 0);

        storedHashInput.Dock = DockStyle.Fill;
        storedHashInput.Multiline = true;
        storedHashInput.ScrollBars = ScrollBars.Vertical;

        verifyPasswordButton.Text = "Verify Password";
        verifyPasswordButton.Width = 150;
        verifyPasswordButton.Click += VerifyPasswordButton_Click;

        verificationResultLabel.Dock = DockStyle.Fill;
        verificationResultLabel.Text = "Result: not checked";
        verificationResultLabel.TextAlign = ContentAlignment.MiddleLeft;

        layout.Controls.Add(CreateLabel("Password"), 0, 0);
        layout.Controls.Add(passwordRow, 1, 0);
        layout.Controls.Add(CreateLabel("Stored hash"), 0, 1);
        layout.Controls.Add(storedHashInput, 1, 1);
        layout.Controls.Add(new Panel { Dock = DockStyle.Fill }, 0, 2);
        layout.Controls.Add(verifyPasswordButton, 1, 2);
        layout.Controls.Add(new Panel { Dock = DockStyle.Fill }, 0, 3);
        layout.Controls.Add(verificationResultLabel, 1, 3);

        group.Controls.Add(layout);
        return group;
    }

    private GroupBox CreateInspectorSection()
    {
        var group = CreateGroupBox("Hash inspector / debug");

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(8)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        inspectHashButton.Text = "Inspect Hash";
        inspectHashButton.Width = 130;
        inspectHashButton.Click += InspectHashButton_Click;

        inspectionGrid.Dock = DockStyle.Fill;
        inspectionGrid.AllowUserToAddRows = false;
        inspectionGrid.AllowUserToDeleteRows = false;
        inspectionGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        inspectionGrid.BackgroundColor = SystemColors.Window;
        inspectionGrid.ReadOnly = true;
        inspectionGrid.RowHeadersVisible = false;
        inspectionGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        inspectionGrid.Columns.Add("Property", "Property");
        inspectionGrid.Columns.Add("Value", "Value");
        inspectionGrid.Columns[0].FillWeight = 32;
        inspectionGrid.Columns[1].FillWeight = 68;

        layout.Controls.Add(inspectHashButton, 0, 0);
        layout.Controls.Add(inspectionGrid, 0, 1);

        group.Controls.Add(layout);
        return group;
    }

    private static GroupBox CreateGroupBox(string text)
    {
        return new GroupBox
        {
            Text = text,
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
            Margin = new Padding(0, 0, 0, 12)
        };
    }

    private static TableLayoutPanel CreateSectionGrid()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(8)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return layout;
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

    private void LoadAlgorithmOptions()
    {
        var options = registry.GetAvailableHashers()
            .Select(ToOption)
            .ToList();

        algorithmComboBox.DataSource = options;
        algorithmComboBox.DisplayMember = nameof(HashAlgorithmOption.DisplayName);
        algorithmComboBox.ValueMember = nameof(HashAlgorithmOption.AlgorithmName);

        var defaultOption = options.FirstOrDefault(x => x.AlgorithmName == PasswordHasherNames.Argon2id)
            ?? options.FirstOrDefault(x => x.AlgorithmName == PasswordHasherNames.Pbkdf2Sha256);

        if (defaultOption is not null)
        {
            algorithmComboBox.SelectedItem = defaultOption;
        }
    }

    private static HashAlgorithmOption ToOption(PasswordHasherDescriptor descriptor)
    {
        var isRecommended = descriptor.SecurityCategory is PasswordHasherSecurityCategory.ProductionSafe
            or PasswordHasherSecurityCategory.FrameworkFormat;
        var suffix = descriptor.SecurityCategory == PasswordHasherSecurityCategory.EducationalOnly
            ? " (educational only)"
            : descriptor.IsDefaultRecommendation
                ? " (recommended)"
                : string.Empty;

        return new HashAlgorithmOption
        {
            AlgorithmName = descriptor.AlgorithmName,
            DisplayName = $"{descriptor.AlgorithmName}{suffix}",
            IsRecommendedForPasswordStorage = isRecommended,
            Description = descriptor.Description
        };
    }

    private void UpdateAlgorithmDescription()
    {
        if (algorithmComboBox.SelectedItem is not HashAlgorithmOption option)
        {
            algorithmDescriptionLabel.Text = string.Empty;
            return;
        }

        algorithmDescriptionLabel.Text = option.Description;
        algorithmDescriptionLabel.ForeColor = option.IsRecommendedForPasswordStorage
            ? Color.DarkGreen
            : Color.DarkRed;
    }

    private void GenerateHashButton_Click(object? sender, EventArgs e)
    {
        if (!TryGetSelectedHasher(out var hasher) || !ValidateRequired(passwordInput.Text, "Enter a password to hash."))
        {
            return;
        }

        try
        {
            var hash = hasher.HashPassword(passwordInput.Text);
            generatedHashTextBox.Text = hash;
            RenderGeneratedBase64Components(hash);
            storedHashInput.Text = hash;
            verificationResultLabel.Text = "Result: generated hash copied to verification input";
            verificationResultLabel.ForeColor = SystemColors.ControlText;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            ShowError(ex.Message);
        }
    }

    private void CopyHashButton_Click(object? sender, EventArgs e)
    {
        if (!ValidateRequired(generatedHashTextBox.Text, "Generate a hash before copying."))
        {
            return;
        }

        Clipboard.SetText(generatedHashTextBox.Text);
    }

    private void VerifyPasswordButton_Click(object? sender, EventArgs e)
    {
        if (!ValidateRequired(verifyPasswordInput.Text, "Enter the password to verify.")
            || !ValidateRequired(storedHashInput.Text, "Paste or generate a stored hash to verify."))
        {
            return;
        }

        try
        {
            var verified = VerifyAgainstAnyKnownHasher(verifyPasswordInput.Text, storedHashInput.Text);
            verificationResultLabel.Text = verified ? "Result: password verified" : "Result: password does not match";
            verificationResultLabel.ForeColor = verified ? Color.DarkGreen : Color.DarkRed;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            ShowError(ex.Message);
        }
    }

    private void InspectHashButton_Click(object? sender, EventArgs e)
    {
        var hashToInspect = !string.IsNullOrWhiteSpace(storedHashInput.Text)
            ? storedHashInput.Text
            : generatedHashTextBox.Text;

        if (!ValidateRequired(hashToInspect, "Paste or generate a hash before inspecting."))
        {
            return;
        }

        var info = hashInspector.Inspect(hashToInspect);
        RenderInspection(info);
    }

    private bool TryGetSelectedHasher(out IPasswordHasher hasher)
    {
        hasher = null!;

        if (algorithmComboBox.SelectedItem is not HashAlgorithmOption option)
        {
            ShowError("Select a hashing algorithm.");
            return false;
        }

        hasher = hasherFactory.Create(option.AlgorithmName);
        return true;
    }

    private bool VerifyAgainstAnyKnownHasher(string password, string storedHash)
    {
        foreach (var descriptor in registry.GetAvailableHashers())
        {
            var hasher = hasherFactory.Create(descriptor.AlgorithmName);
            if (hasher.VerifyPassword(password, storedHash))
            {
                return true;
            }
        }

        return false;
    }

    private void RenderInspection(PasswordHashInfo info)
    {
        inspectionGrid.Rows.Clear();
        AddInspectionRow("Algorithm name", info.AlgorithmName);
        AddInspectionRow("Version", info.Version);
        AddInspectionRow("Salt (Base64)", info.Salt);
        AddInspectionRow("Hash value (Base64)", info.Hash);
        AddInspectionRow("Iterations", info.Iterations);
        AddInspectionRow("Work factor", info.WorkFactor);
        AddInspectionRow("Memory cost", info.MemoryCost);
        AddInspectionRow("Parallelism", info.Parallelism);
        AddInspectionRow("Hash size", info.HashSize);
        AddInspectionRow("Secure for password storage", info.IsSecureForPasswordStorage ? "Yes" : "No");
        AddInspectionRow("Notes / warnings", info.Notes);
    }

    private void RenderGeneratedBase64Components(string storedHash)
    {
        var info = hashInspector.Inspect(storedHash);
        if (!UsesBase64ComponentEncoding(info))
        {
            generatedSaltBase64TextBox.Text = string.Empty;
            generatedHashBase64TextBox.Text = string.Empty;
            return;
        }

        generatedSaltBase64TextBox.Text = info.Salt ?? string.Empty;
        generatedHashBase64TextBox.Text = info.Hash ?? string.Empty;
    }

    private static bool UsesBase64ComponentEncoding(PasswordHashInfo info)
    {
        return !info.AlgorithmName.StartsWith("bcrypt", StringComparison.OrdinalIgnoreCase)
            && (!string.IsNullOrWhiteSpace(info.Salt) || !string.IsNullOrWhiteSpace(info.Hash));
    }

    private void AddInspectionRow(string property, object? value)
    {
        inspectionGrid.Rows.Add(property, value?.ToString() ?? string.Empty);
    }

    private static bool ValidateRequired(string value, string message)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        MessageBox.Show(message, "Password Hashing Tool", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
    }

    private void InitializeComponent()
    {

    }

    private static void ShowError(string message)
    {
        MessageBox.Show(message, "Password Hashing Tool", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
