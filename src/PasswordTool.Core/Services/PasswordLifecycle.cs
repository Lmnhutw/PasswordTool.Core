using PasswordTool.Core.Models;

namespace PasswordTool.Core.Services;

internal static class PasswordLifecycle
{
    public static DateTimeOffset GetEffectivePasswordChangedAt(VaultItem item, DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(item);
        var now = utcNow.ToUniversalTime();

        if (IsUsable(item.PasswordChangedAt, now)) return item.PasswordChangedAt!.Value.ToUniversalTime();

        var historyDate = item.PasswordHistory
            .Where(entry => entry is not null && IsUsable(entry.ChangedAt, now))
            .Select(entry => entry.ChangedAt.ToUniversalTime())
            .DefaultIfEmpty()
            .Max();
        if (historyDate != default) return historyDate;

        if (IsUsable(item.UpdatedAt, now)) return item.UpdatedAt.ToUniversalTime();
        return IsUsable(item.CreatedAt, now) ? item.CreatedAt.ToUniversalTime() : now;
    }

    private static bool IsUsable(DateTimeOffset? value, DateTimeOffset now) =>
        value is { } timestamp && IsUsable(timestamp, now);

    private static bool IsUsable(DateTimeOffset value, DateTimeOffset now) =>
        value != default && value.ToUniversalTime() <= now;
}