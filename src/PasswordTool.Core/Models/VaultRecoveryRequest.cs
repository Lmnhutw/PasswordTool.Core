namespace PasswordTool.Core.Models;

public sealed class VaultRecoveryRequest
{
    public required string BackupJson { get; init; }

    public required string BackupPassphrase { get; init; }

    public required string NewMasterPassword { get; init; }

    public required string NewTotpSecretBase32 { get; init; }

    public required string TotpConfirmationCode { get; init; }

    public override string ToString() => nameof(VaultRecoveryRequest);
}
