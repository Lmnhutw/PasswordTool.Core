using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using PasswordTool.Core.Models;
using OtpNet;

namespace PasswordTool.Core.Services;

public sealed class TrustedUnlockTokenService
{
    private const string ProtectionName = "Windows-DPAPI-CurrentUser";
    private const int CryptprotectUiForbidden = 0x1;
    private static readonly byte[] AuthenticatorPurpose = Encoding.UTF8.GetBytes("PasswordTool/v2/trusted-authenticator");
    private static readonly byte[] VaultKeyPurpose = Encoding.UTF8.GetBytes("PasswordTool/v2/trusted-vault-key");

    public TrustedUnlockToken CreateToken(byte[] vaultKey, string authenticatorSecret, byte[] configFingerprint,
        DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        ArgumentNullException.ThrowIfNull(vaultKey);
        ArgumentNullException.ThrowIfNull(authenticatorSecret);
        ArgumentNullException.ThrowIfNull(configFingerprint);
        if (expiresAt <= createdAt) throw new ArgumentException("Token expiration must be later than token creation.", nameof(expiresAt));

        var secretBytes = Base32Encoding.ToBytes(authenticatorSecret);
        try
        {
            return new TrustedUnlockToken
            {
                Version = 2,
                Protection = ProtectionName,
                ConfigFingerprintBase64 = Convert.ToBase64String(configFingerprint),
                ProtectedAuthenticatorSecretBase64 = Convert.ToBase64String(Protect(secretBytes, BuildEntropy(configFingerprint, AuthenticatorPurpose))),
                ProtectedVaultKeyBase64 = Convert.ToBase64String(Protect(vaultKey, BuildEntropy(configFingerprint, VaultKeyPurpose))),
                CreatedAt = createdAt,
                ExpiresAt = expiresAt
            };
        }
        finally { CryptographicOperations.ZeroMemory(secretBytes); }
    }

    public bool TryUnprotectAuthenticatorSecret(TrustedUnlockToken token, byte[] expectedConfigFingerprint,
        DateTimeOffset now, out byte[] authenticatorSecret, out string errorMessage)
    {
        authenticatorSecret = [];
        if (!ValidateToken(token, expectedConfigFingerprint, now, out errorMessage)) return false;
        try
        {
            authenticatorSecret = Unprotect(Convert.FromBase64String(token.ProtectedAuthenticatorSecretBase64),
                BuildEntropy(expectedConfigFingerprint, AuthenticatorPurpose));
            if (authenticatorSecret.Length < 10) throw new CryptographicException("The trusted Authenticator secret is invalid.");
            return true;
        }
        catch (Exception ex) when (IsTokenException(ex))
        {
            if (authenticatorSecret.Length > 0) CryptographicOperations.ZeroMemory(authenticatorSecret);
            authenticatorSecret = [];
            errorMessage = TokenFailureMessage;
            return false;
        }
    }

    public bool TryUnprotectVaultKey(TrustedUnlockToken token, byte[] expectedConfigFingerprint,
        DateTimeOffset now, out byte[] vaultKey, out string errorMessage)
    {
        vaultKey = [];
        if (!ValidateToken(token, expectedConfigFingerprint, now, out errorMessage)) return false;
        try
        {
            vaultKey = Unprotect(Convert.FromBase64String(token.ProtectedVaultKeyBase64),
                BuildEntropy(expectedConfigFingerprint, VaultKeyPurpose));
            if (vaultKey.Length != 32) throw new CryptographicException("The trusted vault key is invalid.");
            return true;
        }
        catch (Exception ex) when (IsTokenException(ex))
        {
            if (vaultKey.Length > 0) CryptographicOperations.ZeroMemory(vaultKey);
            vaultKey = [];
            errorMessage = TokenFailureMessage;
            return false;
        }
    }

    public bool IsTokenUsable(TrustedUnlockToken token, byte[] expectedConfigFingerprint, DateTimeOffset now)
    {
        if (!TryUnprotectAuthenticatorSecret(token, expectedConfigFingerprint, now, out var secret, out _)) return false;
        CryptographicOperations.ZeroMemory(secret);
        return true;
    }

    private static bool ValidateToken(TrustedUnlockToken token, byte[] expectedConfigFingerprint,
        DateTimeOffset now, out string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(expectedConfigFingerprint);
        errorMessage = string.Empty;
        try
        {
            if (token.Version != 2 || !string.Equals(token.Protection, ProtectionName, StringComparison.Ordinal))
            {
                errorMessage = "The Google Authenticator login token is not supported. Enter the Master Password to upgrade this vault.";
                return false;
            }
            if (token.ExpiresAt <= now)
            {
                errorMessage = "The Google Authenticator login token has expired. Enter the Master Password to create a new 1-day token.";
                return false;
            }
            var fingerprint = Convert.FromBase64String(token.ConfigFingerprintBase64);
            if (!CryptographicOperations.FixedTimeEquals(fingerprint, expectedConfigFingerprint))
            {
                errorMessage = "The Google Authenticator login token does not match this vault.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(token.ProtectedAuthenticatorSecretBase64)
                || string.IsNullOrWhiteSpace(token.ProtectedVaultKeyBase64))
            {
                errorMessage = TokenFailureMessage;
                return false;
            }
            return true;
        }
        catch (Exception ex) when (IsTokenException(ex))
        {
            errorMessage = TokenFailureMessage;
            return false;
        }
    }

    private static byte[] BuildEntropy(byte[] fingerprint, byte[] purpose)
    {
        var input = new byte[fingerprint.Length + purpose.Length];
        Buffer.BlockCopy(fingerprint, 0, input, 0, fingerprint.Length);
        Buffer.BlockCopy(purpose, 0, input, fingerprint.Length, purpose.Length);
        try { return SHA256.HashData(input); }
        finally { CryptographicOperations.ZeroMemory(input); }
    }

    private const string TokenFailureMessage = "The Google Authenticator login token could not be used. Enter the Master Password to create a new 1-day token.";
    private static bool IsTokenException(Exception ex) => ex is ArgumentException or FormatException
        or CryptographicException or InvalidOperationException or Win32Exception;
    private static byte[] Protect(byte[] plaintext, byte[] entropy)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Google Authenticator login tokens require Windows DPAPI.");
        }

        var input = DataBlob.FromBytes(plaintext);
        var entropyBlob = DataBlob.FromBytes(entropy);
        var output = default(DataBlob);

        try
        {
            if (!CryptProtectData(ref input, "PasswordTool trusted unlock token", ref entropyBlob, IntPtr.Zero, IntPtr.Zero, CryptprotectUiForbidden, ref output))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return output.ToArray();
        }
        finally
        {
            input.FreeHGlobal(zeroMemory: true);
            entropyBlob.FreeHGlobal(zeroMemory: true);
            output.FreeLocal(zeroMemory: false);
        }
    }

    private static byte[] Unprotect(byte[] protectedData, byte[] entropy)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Google Authenticator login tokens require Windows DPAPI.");
        }

        var input = DataBlob.FromBytes(protectedData);
        var entropyBlob = DataBlob.FromBytes(entropy);
        var output = default(DataBlob);

        try
        {
            if (!CryptUnprotectData(ref input, IntPtr.Zero, ref entropyBlob, IntPtr.Zero, IntPtr.Zero, CryptprotectUiForbidden, ref output))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return output.ToArray();
        }
        finally
        {
            input.FreeHGlobal(zeroMemory: false);
            entropyBlob.FreeHGlobal(zeroMemory: true);
            output.FreeLocal(zeroMemory: true);
        }
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(
        ref DataBlob pDataIn,
        string szDataDescr,
        ref DataBlob pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        int dwFlags,
        ref DataBlob pDataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectData(
        ref DataBlob pDataIn,
        IntPtr ppszDataDescr,
        ref DataBlob pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        int dwFlags,
        ref DataBlob pDataOut);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int cbData;

        public IntPtr pbData;

        public static DataBlob FromBytes(byte[] value)
        {
            ArgumentNullException.ThrowIfNull(value);

            if (value.Length == 0)
            {
                return new DataBlob();
            }

            var pointer = Marshal.AllocHGlobal(value.Length);
            Marshal.Copy(value, 0, pointer, value.Length);

            return new DataBlob
            {
                cbData = value.Length,
                pbData = pointer
            };
        }

        public readonly byte[] ToArray()
        {
            if (cbData <= 0 || pbData == IntPtr.Zero)
            {
                return [];
            }

            var value = new byte[cbData];
            Marshal.Copy(pbData, value, 0, cbData);
            return value;
        }

        public void FreeHGlobal(bool zeroMemory)
        {
            if (pbData == IntPtr.Zero)
            {
                return;
            }

            if (zeroMemory && cbData > 0)
            {
                var zeroes = new byte[cbData];
                Marshal.Copy(zeroes, 0, pbData, cbData);
            }

            Marshal.FreeHGlobal(pbData);
            pbData = IntPtr.Zero;
            cbData = 0;
        }

        public void FreeLocal(bool zeroMemory)
        {
            if (pbData == IntPtr.Zero)
            {
                return;
            }

            if (zeroMemory && cbData > 0)
            {
                var zeroes = new byte[cbData];
                Marshal.Copy(zeroes, 0, pbData, cbData);
            }

            LocalFree(pbData);
            pbData = IntPtr.Zero;
            cbData = 0;
        }
    }
}
