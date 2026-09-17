namespace PasswordTool.Core.Models;

public sealed class PasswordHistoryEntry
{
    public string Password { get; set; } = string.Empty;

    public DateTimeOffset ChangedAt { get; set; }
}
