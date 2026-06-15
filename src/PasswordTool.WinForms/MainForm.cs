using PasswordTool.Core.Services;

namespace PasswordTool.WinForms;

public partial class MainForm : Form
{
    private readonly EncryptionService encryptionService = new();
    private readonly TotpService totpService = new();
    private readonly VaultStorageService storageService = new();
    private readonly VaultService vaultService;
    private readonly Label statusLabel = new();

    private bool startupFlowStarted;

    public MainForm()
    {
        vaultService = new VaultService(storageService, encryptionService, totpService);
        InitializeComponent();
        FormIconService.Apply(this);
    }

    private void BuildInterface()
    {
        Controls.Clear();
        Text = "PasswordTool";
        StartPosition = FormStartPosition.CenterScreen;
        Padding = new Padding(20);

        statusLabel.Dock = DockStyle.Fill;
        statusLabel.Text = "Opening PasswordTool...";
        statusLabel.TextAlign = ContentAlignment.MiddleCenter;

        Controls.Add(statusLabel);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        if (startupFlowStarted)
        {
            return;
        }

        startupFlowStarted = true;
        BeginInvoke(new Action(RunStartupFlow));
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        vaultService.Dispose();
        base.OnFormClosed(e);
    }

    private void RunStartupFlow()
    {
        try
        {
            if (vaultService.HasPartialStorage)
            {
                ShowError("PasswordTool found only one storage file. To avoid data loss, it will not overwrite partial storage. Check the files under:\n\n" + vaultService.StorageDirectory);
                Close();
                return;
            }

            if (!vaultService.IsInitialized)
            {
                RunFirstLaunchFlow();
            }
            else
            {
                RunExistingVaultFlow();
            }
        }
        catch (Exception ex) when (ex is ArgumentException
            or InvalidOperationException
            or UnauthorizedAccessException
            or IOException)
        {
            ShowError(ex.Message);
            Close();
        }
    }

    private void RunFirstLaunchFlow()
    {
        using var createMasterPasswordForm = new CreateMasterPasswordForm();
        if (createMasterPasswordForm.ShowDialog(this) != DialogResult.OK)
        {
            Close();
            return;
        }

        var secret = totpService.GenerateSecret();
        var otpAuthUri = totpService.CreateOtpAuthUri(secret, "PasswordTool", Environment.UserName);

        using var setupAuthenticatorForm = new SetupAuthenticatorForm(secret, otpAuthUri, totpService);
        if (setupAuthenticatorForm.ShowDialog(this) != DialogResult.OK)
        {
            Close();
            return;
        }

        vaultService.InitializeNewVault(
            createMasterPasswordForm.MasterPassword,
            secret,
            setupAuthenticatorForm.VerifiedCode);

        OpenVaultForm();
    }

    private void RunExistingVaultFlow()
    {
        while (!IsDisposed)
        {
            using var unlockVaultForm = new UnlockVaultForm();
            if (unlockVaultForm.ShowDialog(this) != DialogResult.OK)
            {
                Close();
                return;
            }

            if (!vaultService.TryUnlockMasterPassword(unlockVaultForm.MasterPassword, out var errorMessage))
            {
                ShowError(errorMessage);
                continue;
            }

            if (!vaultService.IsGoogleAuthenticatorConfigured)
            {
                OpenVaultForm();
                return;
            }

            using var verifyTotpForm = new VerifyTotpForm(
                "Google Authenticator",
                "Enter your 6-digit Google Authenticator code to open the vault.",
                vaultService.VerifyTotpForSession);

            if (verifyTotpForm.ShowDialog(this) == DialogResult.OK)
            {
                OpenVaultForm();
                return;
            }

            vaultService.ClearSession();
            Close();
            return;
        }
    }

    private void OpenVaultForm()
    {
        Hide();

        using var vaultForm = new VaultForm(vaultService);
        vaultForm.ShowDialog(this);

        Close();
    }

    private static void ShowError(string message)
    {
        MessageBox.Show(message, "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
