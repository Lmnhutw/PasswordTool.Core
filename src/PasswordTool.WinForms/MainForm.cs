using PasswordTool.Core.Services;
using PasswordTool.Core.Models;
using System.Security.Cryptography;

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
            or IOException
            or InvalidDataException
            or CryptographicException
            or System.Text.Json.JsonException)
        {
            ShowError(ex.Message);
            Close();
        }
    }

    private void RunFirstLaunchFlow()
    {
        using var choiceForm = new FirstLaunchChoiceForm();
        if (choiceForm.ShowDialog(this) != DialogResult.OK)
        {
            Close();
            return;
        }

        if (choiceForm.RecoverFromBackup)
        {
            RunRecoveryFlow();
            return;
        }

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

        if (OpenVaultForm())
        {
            RunExistingVaultFlow();
        }
    }

    private void RunRecoveryFlow()
    {
        using var openDialog = new OpenFileDialog
        {
            Title = "Recover from Encrypted PasswordTool Backup",
            Filter = "PasswordTool JSON backup (*.json)|*.json",
            CheckFileExists = true
        };
        if (openDialog.ShowDialog(this) != DialogResult.OK)
        {
            Close();
            return;
        }

        var backupJson = ReadBoundedBackupFile(openDialog.FileName);
        using var passphraseForm = new BackupPassphraseForm(requireConfirmation: false);
        if (passphraseForm.ShowDialog(this) != DialogResult.OK)
        {
            Close();
            return;
        }

        var inspection = vaultService.InspectBackupJson(backupJson, passphraseForm.Passphrase);
        var summary = $"Backup format: {inspection.Format} v{inspection.Version}\n" +
            $"Created: {FormatTimestamp(inspection.CreatedAt)}\n" +
            $"Items: {inspection.TotalItemCount} ({inspection.PasswordItemCount} passwords, " +
            $"{inspection.RecoveryCodeItemCount} recovery-code sets)\n" +
            $"Active: {inspection.ActiveItemCount}; Trash: {inspection.TrashItemCount}\n\n" +
            "Continue to create a new Master Password and a new PasswordTool Authenticator? " +
            "The old trusted token and application Authenticator configuration are not in this backup.";
        if (MessageBox.Show(summary, "Verified Backup", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes)
        {
            Close();
            return;
        }

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

        vaultService.RecoverFromBackup(new VaultRecoveryRequest
        {
            BackupJson = backupJson,
            BackupPassphrase = passphraseForm.Passphrase,
            NewMasterPassword = createMasterPasswordForm.MasterPassword,
            NewTotpSecretBase32 = secret,
            TotpConfirmationCode = setupAuthenticatorForm.VerifiedCode
        });

        if (OpenVaultForm()) RunExistingVaultFlow();
    }

    private static string ReadBoundedBackupFile(string path)
    {
        var info = new FileInfo(path);
        if (info.Length > VaultBackupService.MaxBackupJsonCharacters)
        {
            throw new InvalidDataException("The selected backup exceeds the 10 MB limit.");
        }

        return File.ReadAllText(path);
    }

    private static string FormatTimestamp(DateTimeOffset? value) =>
        value.HasValue ? value.Value.ToLocalTime().ToString("g") : "Not available";

    private void RunExistingVaultFlow()
    {
        while (!IsDisposed)
        {
            using var unlockVaultForm = new UnlockVaultForm(
                vaultService.CanUnlockWithGoogleAuthenticatorToken,
                vaultService.LoginMode);
            if (unlockVaultForm.ShowDialog(this) != DialogResult.OK)
            {
                Close();
                return;
            }

            if (unlockVaultForm.UseGoogleAuthenticatorLogin)
            {
                if (!vaultService.TryUnlockWithGoogleAuthenticator(unlockVaultForm.GoogleAuthenticatorCode, out var googleAuthenticatorError))
                {
                    ShowWarning(googleAuthenticatorError);
                    continue;
                }

                if (OpenVaultForm())
                {
                    continue;
                }
                return;
            }

            if (!vaultService.TryUnlockMasterPassword(unlockVaultForm.MasterPassword, out var errorMessage))
            {
                ShowError(errorMessage);
                continue;
            }

            if (OpenVaultForm())
            {
                continue;
            }
            return;
        }
    }

    private bool OpenVaultForm()
    {
        Hide();

        using var vaultForm = new VaultForm(vaultService);
        vaultForm.ShowDialog(this);

        if (vaultForm.LockRequested && !IsDisposed)
        {
            Show();
            return true;
        }

        Close();
        return false;
    }

    private static void ShowError(string message)
    {
        MessageBox.Show(message, "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private static void ShowWarning(string message)
    {
        MessageBox.Show(message, "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
