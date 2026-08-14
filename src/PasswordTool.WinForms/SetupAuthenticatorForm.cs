using PasswordTool.Core.Services;
using QRCoder;

namespace PasswordTool.WinForms;

public sealed class SetupAuthenticatorForm : Form
{
    private readonly string otpAuthUri;
    private readonly TotpService totpService;
    private readonly PictureBox qrPictureBox = new();
    private readonly TextBox codeTextBox = new();

    public SetupAuthenticatorForm(string secretBase32, string otpAuthUri, TotpService totpService)
    {
        SecretBase32 = secretBase32;
        this.otpAuthUri = otpAuthUri;
        this.totpService = totpService;
        BuildInterface();
        FormIconService.Apply(this);
    }

    public string SecretBase32 { get; }

    public string VerifiedCode { get; private set; } = string.Empty;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            qrPictureBox.Image?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void BuildInterface()
    {
        Text = "Set Up Google Authenticator";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(760, 470);
        Padding = new Padding(16);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 310));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 300));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 16));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        qrPictureBox.Dock = DockStyle.Fill;
        qrPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        qrPictureBox.Image = CreateQrImage(otpAuthUri);

        var instructionLabel = new Label
        {
            Text = "Scan this QR code with Google Authenticator. Other 6-digit TOTP apps are compatible.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var secretTextBox = new TextBox
        {
            Text = SecretBase32,
            Dock = DockStyle.Fill,
            ReadOnly = true,
            Margin = new Padding(0, 3, 8, 0)
        };

        var secretRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1
        };
        secretRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        secretRow.Controls.Add(secretTextBox, 0, 0);
        secretTextBox.ShortcutsEnabled = false;

        var secretBlock = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        secretBlock.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        secretBlock.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        secretBlock.Controls.Add(CreateLabel("Manual secret"), 0, 0);
        secretBlock.Controls.Add(secretRow, 0, 1);

        codeTextBox.Dock = DockStyle.Left;
        codeTextBox.Width = 140;
        codeTextBox.MaxLength = 6;
        codeTextBox.TextAlign = HorizontalAlignment.Center;
        codeTextBox.Margin = new Padding(0, 6, 12, 0);

        var confirmButton = new Button
        {
            Text = "Confirm",
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 8, 0),
            DialogResult = DialogResult.None
        };
        confirmButton.Click += ConfirmButton_Click;

        var cancelButton = new Button
        {
            Text = "Cancel",
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 0),
            DialogResult = DialogResult.Cancel
        };

        var codeRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1
        };
        codeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115));
        codeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155));
        codeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
        codeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        codeRow.Controls.Add(CreateLabel("6-digit code"), 0, 0);
        codeRow.Controls.Add(codeTextBox, 1, 0);
        codeRow.Controls.Add(confirmButton, 2, 0);
        codeRow.Controls.Add(cancelButton, 3, 0);

        root.Controls.Add(qrPictureBox, 0, 0);
        root.SetRowSpan(qrPictureBox, 4);
        root.Controls.Add(instructionLabel, 1, 0);
        root.Controls.Add(secretBlock, 1, 1);
        root.Controls.Add(codeRow, 1, 3);

        Controls.Add(root);
        AcceptButton = confirmButton;
        CancelButton = cancelButton;
    }

    private void ConfirmButton_Click(object? sender, EventArgs e)
    {
        var code = codeTextBox.Text.Trim();
        if (code.Length != 6 || !code.All(char.IsDigit))
        {
            MessageBox.Show("Enter the 6-digit Google Authenticator code.", "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!totpService.VerifyCode(SecretBase32, code))
        {
            MessageBox.Show("Invalid Google Authenticator code. Check the device time and try again.", "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            codeTextBox.SelectAll();
            codeTextBox.Focus();
            return;
        }

        VerifiedCode = code;
        DialogResult = DialogResult.OK;
        Close();
    }

    private static Image CreateQrImage(string value)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(value, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(data);
        var pngBytes = qrCode.GetGraphic(12);

        using var stream = new MemoryStream(pngBytes);
        using var image = Image.FromStream(stream);
        return new Bitmap(image);
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
