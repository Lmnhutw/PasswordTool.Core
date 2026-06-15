using System.Security.Cryptography;
using System.Text;
using System.ComponentModel;
using PasswordTool.Core.Models;

namespace PasswordTool.Core.Services;

public sealed class VaultService : IDisposable
{
    private readonly EncryptionService encryptionService;
    private readonly MasterPasswordService masterPasswordService;
    private readonly TotpService totpService;
    private readonly TrustedUnlockTokenService trustedUnlockTokenService;
    private readonly VaultStorageService storageService;
    private readonly Func<DateTimeOffset> utcNow;

    private byte[]? encryptionKey;
    private string? totpSecretBase32;
    private VaultData? vaultData;
    private bool isVaultOpen;
    private bool disposed;

    public VaultService()
        : this(new VaultStorageService(), new EncryptionService(), new TotpService())
    {
    }

    public VaultService(
        VaultStorageService storageService,
        EncryptionService encryptionService,
        TotpService totpService,
        TrustedUnlockTokenService? trustedUnlockTokenService = null,
        Func<DateTimeOffset>? utcNow = null)
    {
        this.storageService = storageService;
        this.encryptionService = encryptionService;
        this.totpService = totpService;
        this.trustedUnlockTokenService = trustedUnlockTokenService ?? new TrustedUnlockTokenService();
        this.utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        masterPasswordService = new MasterPasswordService(encryptionService);
    }

    public bool IsInitialized => storageService.IsInitialized;

    public bool HasPartialStorage => storageService.HasPartialStorage;

    public string StorageDirectory => storageService.AppDirectory;

    public string ConfigPath => storageService.ConfigPath;

    public string VaultPath => storageService.VaultPath;

    public string TrustedUnlockTokenPath => storageService.TrustedUnlockTokenPath;

    public bool IsGoogleAuthenticatorConfigured
    {
        get
        {
            ThrowIfDisposed();
            return !string.IsNullOrWhiteSpace(totpSecretBase32);
        }
    }

    public bool CanUnlockWithGoogleAuthenticatorToken
    {
        get
        {
            ThrowIfDisposed();

            try
            {
                var config = storageService.LoadConfig();
                if (string.IsNullOrWhiteSpace(config.EncryptedTotpSecret) || !storageService.HasTrustedUnlockToken)
                {
                    return false;
                }

                var token = storageService.LoadTrustedUnlockToken();
                return trustedUnlockTokenService.IsTokenUsable(token, ComputeConfigFingerprint(config), utcNow());
            }
            catch (Exception ex) when (ex is IOException
                or UnauthorizedAccessException
                or InvalidOperationException
                or CryptographicException
                or FormatException
                or System.Text.Json.JsonException
                or PlatformNotSupportedException)
            {
                return false;
            }
        }
    }

    public void InitializeNewVault(string masterPassword, string totpSecretBase32, string confirmationTotpCode)
    {
        ThrowIfDisposed();

        if (storageService.HasConfig || storageService.HasVault)
        {
            throw new InvalidOperationException("PasswordTool storage already exists.");
        }

        if (!totpService.IsSecretValid(totpSecretBase32))
        {
            throw new ArgumentException("The generated TOTP secret is invalid.", nameof(totpSecretBase32));
        }

        if (!totpService.VerifyCode(totpSecretBase32, confirmationTotpCode))
        {
            throw new UnauthorizedAccessException("Authenticator setup could not be verified.");
        }

        var config = masterPasswordService.CreateConfig(masterPassword, totpSecretBase32);
        var key = masterPasswordService.DeriveKey(masterPassword, config);

        ClearSession();

        encryptionKey = key;
        this.totpSecretBase32 = totpSecretBase32;
        vaultData = new VaultData();
        isVaultOpen = true;

        storageService.SaveConfig(config);
        SaveVault();
        SaveTrustedUnlockToken(config);
    }

    public bool TryUnlockMasterPassword(string masterPassword, out string errorMessage)
    {
        ThrowIfDisposed();
        ClearSession();
        errorMessage = string.Empty;

        try
        {
            var config = storageService.LoadConfig();
            if (!masterPasswordService.TryUnlockConfig(
                masterPassword,
                config,
                out var derivedKey,
                out var decryptedTotpSecret))
            {
                errorMessage = "The Master Password is incorrect.";
                return false;
            }

            var encryptedVaultJson = storageService.LoadVaultPayload();
            var decryptedVault = encryptionService.DecryptObject<VaultData>(encryptedVaultJson, derivedKey);
            decryptedVault.Items ??= [];

            encryptionKey = derivedKey;
            totpSecretBase32 = string.IsNullOrWhiteSpace(decryptedTotpSecret)
                ? null
                : decryptedTotpSecret;
            vaultData = decryptedVault;
            isVaultOpen = true;
            SaveTrustedUnlockToken(config);
            return true;
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or CryptographicException
            or FormatException
            or PlatformNotSupportedException
            or System.Text.Json.JsonException)
        {
            ClearSession();
            errorMessage = "The Master Password is incorrect, or PasswordTool storage could not be opened.";
            return false;
        }
    }

    public bool TryUnlockWithGoogleAuthenticator(string code, out string errorMessage)
    {
        ThrowIfDisposed();
        ClearSession();
        errorMessage = string.Empty;
        byte[]? trustedKey = null;

        if (string.IsNullOrWhiteSpace(code))
        {
            errorMessage = "Google Authenticator code is required.";
            return false;
        }

        try
        {
            var config = storageService.LoadConfig();
            if (string.IsNullOrWhiteSpace(config.EncryptedTotpSecret))
            {
                errorMessage = "Google Authenticator login is not configured for this vault.";
                return false;
            }

            if (!storageService.HasTrustedUnlockToken)
            {
                errorMessage = "Enter the Master Password to create a 1-day Google Authenticator login token.";
                return false;
            }

            var configFingerprint = ComputeConfigFingerprint(config);
            var token = storageService.LoadTrustedUnlockToken();
            if (!trustedUnlockTokenService.TryUnprotectEncryptionKey(
                token,
                configFingerprint,
                utcNow(),
                out trustedKey,
                out errorMessage))
            {
                TryDeleteTrustedUnlockToken();
                return false;
            }

            var decryptedTotpSecret = encryptionService.DecryptString(config.EncryptedTotpSecret, trustedKey);
            if (!totpService.VerifyCode(decryptedTotpSecret, code))
            {
                errorMessage = "Invalid Google Authenticator code.";
                return false;
            }

            var encryptedVaultJson = storageService.LoadVaultPayload();
            var decryptedVault = encryptionService.DecryptObject<VaultData>(encryptedVaultJson, trustedKey);
            decryptedVault.Items ??= [];

            encryptionKey = trustedKey;
            trustedKey = null;
            totpSecretBase32 = decryptedTotpSecret;
            vaultData = decryptedVault;
            isVaultOpen = true;
            return true;
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or CryptographicException
            or FormatException
            or System.Text.Json.JsonException)
        {
            ClearSession();
            errorMessage = "Google Authenticator login could not open PasswordTool storage. Enter the Master Password to create a new 1-day token.";
            return false;
        }
        finally
        {
            if (trustedKey is { Length: > 0 })
            {
                CryptographicOperations.ZeroMemory(trustedKey);
            }
        }
    }

    public bool VerifyTotpForSession(string code)
    {
        ThrowIfDisposed();
        EnsureMasterPasswordUnlocked();

        if (totpSecretBase32 is null)
        {
            isVaultOpen = true;
            return true;
        }

        if (!totpService.VerifyCode(totpSecretBase32!, code))
        {
            return false;
        }

        isVaultOpen = true;
        return true;
    }

    public bool VerifyTotpForSensitiveAction(string code)
    {
        ThrowIfDisposed();
        EnsureOpen();
        if (totpSecretBase32 is null)
        {
            return true;
        }

        return totpService.VerifyCode(totpSecretBase32!, code);
    }

    public IReadOnlyList<VaultItem> GetItems()
    {
        ThrowIfDisposed();
        EnsureOpen();

        return vaultData!.Items
            .OrderBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
            .Select(item => Clone(item, includePassword: false))
            .ToList();
    }

    public VaultItem GetItemForEditing(Guid id, string totpCode)
    {
        ThrowIfDisposed();
        EnsureOpen();
        RequireSensitiveTotp(totpCode);
        return Clone(FindItem(id), includePassword: true);
    }

    public string GetUsername(Guid id)
    {
        ThrowIfDisposed();
        EnsureOpen();
        return FindItem(id).Username;
    }

    public string GetPassword(Guid id, string totpCode)
    {
        ThrowIfDisposed();
        EnsureOpen();
        RequireSensitiveTotp(totpCode);
        return FindItem(id).Password;
    }

    public VaultItem AddItem(VaultItem item)
    {
        ThrowIfDisposed();
        EnsureOpen();
        ValidateVaultItem(item);

        var now = DateTimeOffset.UtcNow;
        var newItem = Clone(item, includePassword: true);
        newItem.Id = Guid.NewGuid();
        newItem.CreatedAt = now;
        newItem.UpdatedAt = now;

        vaultData!.Items.Add(newItem);
        SaveVault();
        return Clone(newItem, includePassword: false);
    }

    public void UpdateItem(VaultItem item)
    {
        ThrowIfDisposed();
        EnsureOpen();
        ValidateVaultItem(item);

        var existing = FindItem(item.Id);
        existing.Title = item.Title.Trim();
        existing.Username = item.Username.Trim();
        existing.Password = item.Password;
        existing.Url = item.Url.Trim();
        existing.Notes = item.Notes;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        SaveVault();
    }

    public void DeleteItem(Guid id)
    {
        ThrowIfDisposed();
        EnsureOpen();

        var item = FindItem(id);
        vaultData!.Items.Remove(item);
        SaveVault();
    }

    public void ClearSession()
    {
        if (encryptionKey is { Length: > 0 })
        {
            CryptographicOperations.ZeroMemory(encryptionKey);
        }

        encryptionKey = null;
        totpSecretBase32 = null;
        vaultData = null;
        isVaultOpen = false;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        ClearSession();
        disposed = true;
    }

    private void SaveVault()
    {
        EnsureMasterPasswordUnlocked();
        storageService.SaveVaultPayload(encryptionService.EncryptObject(vaultData, encryptionKey!));
    }

    private void SaveTrustedUnlockToken(AppConfig config)
    {
        if (encryptionKey is null || string.IsNullOrWhiteSpace(config.EncryptedTotpSecret))
        {
            return;
        }

        try
        {
            var now = utcNow();
            var token = trustedUnlockTokenService.CreateToken(
                encryptionKey,
                ComputeConfigFingerprint(config),
                now,
                now.AddDays(1));
            storageService.SaveTrustedUnlockToken(token);
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or CryptographicException
            or InvalidOperationException
            or NotSupportedException
            or Win32Exception)
        {
            TryDeleteTrustedUnlockToken();
        }
    }

    private void TryDeleteTrustedUnlockToken()
    {
        try
        {
            storageService.DeleteTrustedUnlockToken();
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
        }
    }

    private static byte[] ComputeConfigFingerprint(AppConfig config)
    {
        var fingerprintInput = string.Join('\n',
            config.Version.ToString(System.Globalization.CultureInfo.InvariantCulture),
            config.KdfAlgorithm,
            config.KdfIterations.ToString(System.Globalization.CultureInfo.InvariantCulture),
            config.KeySizeBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
            config.SaltBase64,
            config.EncryptedTotpSecret);

        return SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintInput));
    }

    private VaultItem FindItem(Guid id)
    {
        return vaultData!.Items.FirstOrDefault(item => item.Id == id)
            ?? throw new InvalidOperationException("The selected vault item no longer exists.");
    }

    private void RequireSensitiveTotp(string code)
    {
        if (!VerifyTotpForSensitiveAction(code))
        {
            throw new UnauthorizedAccessException("Authenticator verification failed.");
        }
    }

    private void EnsureMasterPasswordUnlocked()
    {
        if (encryptionKey is null || vaultData is null)
        {
            throw new InvalidOperationException("The vault is not unlocked with the Master Password.");
        }
    }

    private void EnsureOpen()
    {
        EnsureMasterPasswordUnlocked();

        if (!isVaultOpen)
        {
            throw new InvalidOperationException("The vault requires Authenticator verification before it can be opened.");
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    private static void ValidateVaultItem(VaultItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (string.IsNullOrWhiteSpace(item.Title))
        {
            throw new ArgumentException("Title is required.", nameof(item));
        }

        if (string.IsNullOrWhiteSpace(item.Password))
        {
            throw new ArgumentException("Password is required.", nameof(item));
        }
    }

    private static VaultItem Clone(VaultItem item, bool includePassword)
    {
        return new VaultItem
        {
            Id = item.Id,
            Title = item.Title,
            Username = item.Username,
            Password = includePassword ? item.Password : string.Empty,
            Url = item.Url,
            Notes = item.Notes,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        };
    }
}
