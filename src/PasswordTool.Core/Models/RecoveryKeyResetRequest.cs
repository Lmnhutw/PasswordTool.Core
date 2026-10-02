namespace PasswordTool.Core.Models;

public sealed record RecoveryKeyResetRequest(
    string RecoveryKey,
    string NewMasterPassword,
    string NewRecoveryKey,
    bool RecoveryKeySaved,
    string NewTotpSecretBase32,
    string TotpConfirmationCode)
{
    public override string ToString() => nameof(RecoveryKeyResetRequest);
}
