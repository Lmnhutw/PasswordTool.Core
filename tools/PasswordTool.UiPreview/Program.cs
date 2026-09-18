using PasswordTool.Core.Models;
using PasswordTool.Core.Services;
using PasswordTool.WinForms;

namespace PasswordTool.UiPreview;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var outputDirectory = args.Length > 0
            ? Path.GetFullPath(args[0])
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "artifacts", "ui-review"));
        Directory.CreateDirectory(outputDirectory);

        CaptureSingle(
            new UnlockVaultForm(canUseGoogleAuthenticatorLogin: false, VaultLoginMode.Hybrid),
            Path.Combine(outputDirectory, "unlock-unavailable.png"));
        CaptureSingle(
            new UnlockVaultForm(canUseGoogleAuthenticatorLogin: true, VaultLoginMode.Hybrid),
            Path.Combine(outputDirectory, "unlock-available.png"));
        CaptureVault(outputDirectory);
    }

    private static void CaptureVault(string outputDirectory)
    {
        var previewDirectory = Path.Combine(Path.GetTempPath(), $"PasswordTool-UiPreview-{Guid.NewGuid():N}");
        try
        {
            var storage = new VaultStorageService(previewDirectory);
            var encryption = new EncryptionService();
            var totp = new TotpService();
            using var service = new VaultService(storage, encryption, totp);
            var secret = totp.GenerateSecret();
            service.InitializeNewVault(
                "Preview-only master password 2026!",
                secret,
                totp.GetCurrentCode(secret).Code);

            service.AddItem(new VaultItem
            {
                Title = "Cloud Console",
                Username = "jane@example.test",
                Password = "preview-secret-1",
                Folder = "Work",
                Url = "https://console.example.test",
                IsFavorite = true,
                Tags = ["cloud", "work"]
            });
            service.AddItem(new VaultItem
            {
                Title = "Design Workspace",
                Username = "jane.design@example.test",
                Password = "preview-secret-2",
                Folder = "Work",
                Url = "https://design.example.test",
                TotpSecretBase32 = secret
            });
            service.AddItem(new VaultItem
            {
                Title = "Email",
                Username = "jane@example.test",
                Password = "preview-secret-3",
                Folder = "Personal",
                Url = "https://mail.example.test"
            });
            service.AddItem(new VaultItem
            {
                Title = "Home Network",
                Username = "admin",
                Password = "preview-secret-4",
                Folder = "Personal",
                HideUrl = true
            });
            service.AddItem(new VaultItem
            {
                Title = "Recovery Kit",
                Type = VaultItemType.RecoveryCodes,
                Username = "jane@example.test",
                RecoveryCodes = ["PREVIEW-ONE", "PREVIEW-TWO"],
                Folder = "Personal"
            });

            using var form = new VaultForm(service);
            form.ShowInTaskbar = false;
            form.Show();
            Application.DoEvents();
            Capture(form, Path.Combine(outputDirectory, "vault-default.png"));
            form.Size = form.MinimumSize;
            Application.DoEvents();
            Capture(form, Path.Combine(outputDirectory, "vault-minimum.png"));
            form.Close();
        }
        finally
        {
            if (Directory.Exists(previewDirectory))
            {
                Directory.Delete(previewDirectory, recursive: true);
            }
        }
    }

    private static void CaptureSingle(Form form, string path)
    {
        using (form)
        {
            form.ShowInTaskbar = false;
            form.Show();
            Application.DoEvents();
            Capture(form, path);
            form.Close();
        }
    }

    private static void Capture(Form form, string path)
    {
        form.PerformLayout();
        Application.DoEvents();
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }
}
