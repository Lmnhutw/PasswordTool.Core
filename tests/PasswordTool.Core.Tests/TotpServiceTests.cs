using PasswordTool.Core.Services;

namespace PasswordTool.Core.Tests;

public sealed class TotpServiceTests
{
    [Fact]
    public void Website_totp_accepts_base32_and_otpauth_uri()
    {
        var service = new TotpService();
        const string secret = "JBSWY3DPEHPK3PXP";

        Assert.True(service.TryNormalizeWebsiteSecret(secret.ToLowerInvariant(), out var normalized));
        Assert.Equal(secret, normalized);
        Assert.True(service.TryNormalizeWebsiteSecret(
            "otpauth://totp/Example:test?secret=JBSWY3DPEHPK3PXP&issuer=Example",
            out normalized));
        Assert.Equal(secret, normalized);

        var result = service.GetCurrentCode(secret, DateTimeOffset.FromUnixTimeSeconds(1_700_000_000));
        Assert.Matches("^[0-9]{6}$", result.Code);
        Assert.InRange(result.SecondsRemaining, 1, 30);
    }

    [Fact]
    public void Website_totp_rejects_malformed_secrets()
    {
        Assert.False(new TotpService().TryNormalizeWebsiteSecret("not base32!", out _));
    }
}
