using PasswordTool.Core.Models;

namespace PasswordTool.Presentation;

public sealed record UnlockOptions(bool CanUseAuthenticator, VaultLoginMode PreferredLoginMode);

public sealed record AuthenticatorSetup(string SecretBase32, string OtpAuthUri)
{
    public override string ToString() => nameof(AuthenticatorSetup);
}
