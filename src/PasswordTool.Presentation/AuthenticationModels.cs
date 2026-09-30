namespace PasswordTool.Presentation;

public sealed record AuthenticatorSetup(string SecretBase32, string OtpAuthUri)
{
    public override string ToString() => nameof(AuthenticatorSetup);
}
