namespace PasswordTool.Core.Models;

public sealed class VaultData
{
    public int Version { get; set; } = 1;

    public List<VaultItem> Items { get; set; } = [];
}
