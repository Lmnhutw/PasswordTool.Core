using System.Security.Cryptography;
using PasswordTool.Core.Models;

namespace PasswordTool.Core.Services;

public sealed class VaultService : IDisposable
{
    private readonly EncryptionService encryptionService;
    private readonly MasterPasswordService masterPasswordService;
    private readonly TotpService totpService;
    private readonly VaultStorageService storageService;

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
        TotpService totpService)
    {
        this.storageService = storageService;
        this.encryptionService = encryptionService;
        this.totpService = totpService;
        masterPasswordService = new MasterPasswordService(encryptionService);
    }

    public bool IsInitialized => storageService.IsInitialized;

    public bool HasPartialStorage => storageService.HasPartialStorage;

    public string StorageDirectory => storageService.AppDirectory;

    public string ConfigPath => storageService.ConfigPath;

    public string VaultPath => storageService.VaultPath;

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
                errorMessage = "The Master Password is incorrect, or the config file cannot be decrypted.";
                return false;
            }

            var encryptedVaultJson = storageService.LoadVaultPayload();
            var decryptedVault = encryptionService.DecryptObject<VaultData>(encryptedVaultJson, derivedKey);
            decryptedVault.Items ??= [];

            encryptionKey = derivedKey;
            totpSecretBase32 = decryptedTotpSecret;
            vaultData = decryptedVault;
            isVaultOpen = false;
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
            errorMessage = "PasswordTool storage could not be opened. The files may be missing, corrupt, or encrypted with a different Master Password.";
            return false;
        }
    }

    public bool VerifyTotpForSession(string code)
    {
        ThrowIfDisposed();
        EnsureMasterPasswordUnlocked();

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
        if (encryptionKey is null || totpSecretBase32 is null || vaultData is null)
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
