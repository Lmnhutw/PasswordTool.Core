namespace PasswordTool.Core.Models;

public sealed class VaultItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public bool HideUrl { get; set; }

    public string Notes { get; set; } = string.Empty;

    public bool HideNotes { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
