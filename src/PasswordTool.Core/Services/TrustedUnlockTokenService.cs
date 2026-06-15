using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using PasswordTool.Core.Models;

namespace PasswordTool.Core.Services;

public sealed class TrustedUnlockTokenService
{
    private const string ProtectionName = "Windows-DPAPI-CurrentUser";
    private const int CryptprotectUiForbidden = 0x1;

    public TrustedUnlockToken CreateToken(
        byte[] encryptionKey,
        byte[] configFingerprint,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        ArgumentNullException.ThrowIfNull(encryptionKey);
        ArgumentNullException.ThrowIfNull(configFingerprint);

        if (expiresAt <= createdAt)
        {
            throw new ArgumentException("Token expiration must be later than token creation.", nameof(expiresAt));
        }

        return new TrustedUnlockToken
        {
            Version = 1,
            Protection = ProtectionName,
            ConfigFingerprintBase64 = Convert.ToBase64String(configFingerprint),
            ProtectedEncryptionKeyBase64 = Convert.ToBase64String(Protect(encryptionKey, configFingerprint)),
            CreatedAt = createdAt,
            ExpiresAt = expiresAt
        };
    }

    public bool TryUnprotectEncryptionKey(
        TrustedUnlockToken token,
        byte[] expectedConfigFingerprint,
        DateTimeOffset now,
        out byte[] encryptionKey,
        out string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(expectedConfigFingerprint);

        encryptionKey = [];
        errorMessage = string.Empty;

        try
        {
            if (token.Version != 1 || !string.Equals(token.Protection, ProtectionName, StringComparison.Ordinal))
            {
                errorMessage = "The Google Authenticator login token is not supported.";
                return false;
            }

            if (token.ExpiresAt <= now)
            {
                errorMessage = "The Google Authenticator login token has expired. Enter the Master Password to create a new 1-day token.";
                return false;
            }

            var tokenFingerprint = Convert.FromBase64String(token.ConfigFingerprintBase64);
            if (!CryptographicOperations.FixedTimeEquals(tokenFingerprint, expectedConfigFingerprint))
            {
                errorMessage = "The Google Authenticator login token does not match this vault.";
                return false;
            }

            var protectedKey = Convert.FromBase64String(token.ProtectedEncryptionKeyBase64);
            encryptionKey = Unprotect(protectedKey, expectedConfigFingerprint);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException
            or FormatException
            or CryptographicException
            or InvalidOperationException
            or Win32Exception)
        {
            if (encryptionKey.Length > 0)
            {
                CryptographicOperations.ZeroMemory(encryptionKey);
                encryptionKey = [];
            }

            errorMessage = "The Google Authenticator login token could not be used. Enter the Master Password to create a new 1-day token.";
            return false;
        }
    }

    public bool IsTokenUsable(TrustedUnlockToken token, byte[] expectedConfigFingerprint, DateTimeOffset now)
    {
        return TryUnprotectEncryptionKey(token, expectedConfigFingerprint, now, out var encryptionKey, out _)
            && ZeroAndReturnTrue(encryptionKey);
    }

    private static bool ZeroAndReturnTrue(byte[] encryptionKey)
    {
        if (encryptionKey.Length > 0)
        {
            CryptographicOperations.ZeroMemory(encryptionKey);
        }

        return true;
    }

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
