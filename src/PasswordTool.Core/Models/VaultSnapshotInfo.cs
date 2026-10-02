namespace PasswordTool.Core.Models;

public sealed record VaultSnapshotInfo(string Id, DateTimeOffset CreatedAtUtc, bool HasOldSecurityState = false)
{
    public string SecurityStateLabel => HasOldSecurityState ? "Old security state — previous credentials remain valid in this snapshot" : "Current security state";
}
