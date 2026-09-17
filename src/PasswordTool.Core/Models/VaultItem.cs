using System.Text.Json.Serialization;

namespace PasswordTool.Core.Models;

public sealed class VaultItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;

    public VaultItemType Type { get; set; } = VaultItemType.Password;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public List<PasswordHistoryEntry> PasswordHistory { get; set; } = [];

    public string TotpSecretBase32 { get; set; } = string.Empty;

    public List<string> RecoveryCodes { get; set; } = [];

    [JsonIgnore]
    public int RecoveryCodeCount { get; set; }

    [JsonIgnore]
    public bool HasTotp { get; set; }

    public string Url { get; set; } = string.Empty;

    public bool HideUrl { get; set; }

    public string Notes { get; set; } = string.Empty;

    public bool HideNotes { get; set; }

    public bool IsFavorite { get; set; }

    public string Folder { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = [];

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
