using System.Text.Json;
using PasswordTool.Core.Models;

namespace PasswordTool.Core.Services;

public sealed class VaultStorageService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public VaultStorageService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PasswordTool"))
    {
    }

    public VaultStorageService(string appDirectory)
    {
        if (string.IsNullOrWhiteSpace(appDirectory))
        {
            throw new ArgumentException("Application storage directory is required.", nameof(appDirectory));
        }

        AppDirectory = appDirectory;
        ConfigPath = Path.Combine(AppDirectory, ".config");
        VaultPath = Path.Combine(AppDirectory, ".storage");
        TrustedUnlockTokenPath = Path.Combine(AppDirectory, ".trusted-unlock");
    }

    public string AppDirectory { get; }

    public string ConfigPath { get; }

    public string VaultPath { get; }

    public string TrustedUnlockTokenPath { get; }

    public bool HasConfig => File.Exists(ConfigPath);

    public bool HasVault => File.Exists(VaultPath);

    public bool IsInitialized => HasConfig && HasVault;

    public bool HasPartialStorage => HasConfig != HasVault;

    public bool HasTrustedUnlockToken => File.Exists(TrustedUnlockTokenPath);

    public void SaveConfig(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        EnsureStorageDirectory();
        WriteProtectedText(ConfigPath, JsonSerializer.Serialize(config, JsonOptions));
    }

    public AppConfig LoadConfig()
    {
        if (!File.Exists(ConfigPath))
        {
            throw new FileNotFoundException("PasswordTool config file was not found.", ConfigPath);
        }

        var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath), JsonOptions);
        return config ?? throw new InvalidOperationException("PasswordTool config file is empty or invalid.");
    }

    public void SaveTrustedUnlockToken(TrustedUnlockToken token)
    {
        ArgumentNullException.ThrowIfNull(token);
        EnsureStorageDirectory();
        WriteProtectedText(TrustedUnlockTokenPath, JsonSerializer.Serialize(token, JsonOptions));
    }

    public TrustedUnlockToken LoadTrustedUnlockToken()
    {
        if (!File.Exists(TrustedUnlockTokenPath))
        {
            throw new FileNotFoundException("PasswordTool trusted unlock token was not found.", TrustedUnlockTokenPath);
        }

        var token = JsonSerializer.Deserialize<TrustedUnlockToken>(File.ReadAllText(TrustedUnlockTokenPath), JsonOptions);
        return token ?? throw new InvalidOperationException("PasswordTool trusted unlock token is empty or invalid.");
    }

    public void DeleteTrustedUnlockToken()
    {
        if (!File.Exists(TrustedUnlockTokenPath))
        {
            return;
        }

        TryClearProtectedAttributes(TrustedUnlockTokenPath);
        File.Delete(TrustedUnlockTokenPath);
    }

    public void SaveVaultPayload(string encryptedVaultJson)
    {
        if (string.IsNullOrWhiteSpace(encryptedVaultJson))
        {
            throw new ArgumentException("Encrypted vault payload is required.", nameof(encryptedVaultJson));
        }

        EnsureStorageDirectory();
        WriteProtectedText(VaultPath, encryptedVaultJson);
    }

    public string LoadVaultPayload()
    {
        if (!File.Exists(VaultPath))
        {
            throw new FileNotFoundException("PasswordTool vault file was not found.", VaultPath);
        }

        return File.ReadAllText(VaultPath);
    }

    private void EnsureStorageDirectory()
    {
        Directory.CreateDirectory(AppDirectory);
        TryApplyHiddenSystemAttributes(AppDirectory);
    }

    private static void WriteProtectedText(string path, string contents)
    {
        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";

        try
        {
            File.WriteAllText(tempPath, contents);
            TryClearProtectedAttributes(path);
            File.Move(tempPath, path, overwrite: true);
            TryApplyHiddenSystemAttributes(path);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static void TryApplyHiddenSystemAttributes(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            var attributes = File.GetAttributes(path);
            File.SetAttributes(path, attributes | FileAttributes.Hidden | FileAttributes.System);
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
            // File hiding is only an obfuscation layer. Encryption remains the security boundary.
        }
    }

    private static void TryClearProtectedAttributes(string path)
    {
        if (!OperatingSystem.IsWindows() || !File.Exists(path))
        {
            return;
        }

        try
        {
            var attributes = File.GetAttributes(path);
            attributes &= ~FileAttributes.Hidden;
            attributes &= ~FileAttributes.System;
            attributes &= ~FileAttributes.ReadOnly;
            File.SetAttributes(path, attributes);
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
        }
    }
}
