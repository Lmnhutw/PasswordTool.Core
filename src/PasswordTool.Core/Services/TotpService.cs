using System.Security.Cryptography;
using System.Text.RegularExpressions;
using OtpNet;

namespace PasswordTool.Core.Services;

public sealed partial class TotpService
{
    private const int SecretSizeBytes = 20;

    public string GenerateSecret()
    {
        return Base32Encoding.ToString(RandomNumberGenerator.GetBytes(SecretSizeBytes));
    }

    public string CreateOtpAuthUri(string secretBase32, string issuer = "PasswordTool", string? accountName = null)
    {
        var normalizedSecret = NormalizeSecret(secretBase32);
        var normalizedIssuer = string.IsNullOrWhiteSpace(issuer) ? "PasswordTool" : issuer.Trim();
        var normalizedAccount = string.IsNullOrWhiteSpace(accountName)
            ? Environment.UserName
            : accountName.Trim();

        var label = $"{Uri.EscapeDataString(normalizedIssuer)}:{Uri.EscapeDataString(normalizedAccount)}";
        return $"otpauth://totp/{label}?secret={Uri.EscapeDataString(normalizedSecret)}&issuer={Uri.EscapeDataString(normalizedIssuer)}&digits=6&period=30";
    }

    public bool VerifyCode(string secretBase32, string code)
    {
        var normalizedCode = code?.Trim() ?? string.Empty;
        if (!SixDigitCodeRegex().IsMatch(normalizedCode))
        {
            return false;
        }

        try
        {
            var secretBytes = Base32Encoding.ToBytes(NormalizeSecret(secretBase32));
            var totp = new Totp(secretBytes);
            return totp.VerifyTotp(
                normalizedCode,
                out _,
                new VerificationWindow(previous: 1, future: 1));
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            return false;
        }
    }

    public bool IsSecretValid(string secretBase32)
    {
        try
        {
            return Base32Encoding.ToBytes(NormalizeSecret(secretBase32)).Length >= 10;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            return false;
        }
    }

    private static string NormalizeSecret(string secretBase32)
    {
        if (string.IsNullOrWhiteSpace(secretBase32))
        {
            throw new ArgumentException("TOTP secret is required.", nameof(secretBase32));
        }

        return secretBase32.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToUpperInvariant();
    }

    [GeneratedRegex("^\\d{6}$")]
    private static partial Regex SixDigitCodeRegex();
}
