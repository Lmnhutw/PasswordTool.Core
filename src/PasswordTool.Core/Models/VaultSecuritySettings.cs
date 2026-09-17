namespace PasswordTool.Core.Models;

public sealed record VaultSecuritySettings(
    int InactivityLockTimeoutMinutes,
    int SensitiveActionTimeoutMinutes)
{
    public const int DefaultInactivityLockTimeoutMinutes = 10;
    public const int DefaultSensitiveActionTimeoutMinutes = 5;
    public const int MinimumTimeoutMinutes = 1;
    public const int MaximumInactivityLockTimeoutMinutes = 120;
    public const int MaximumSensitiveActionTimeoutMinutes = 30;

    public static void Validate(int inactivityLockTimeoutMinutes, int sensitiveActionTimeoutMinutes)
    {
        if (inactivityLockTimeoutMinutes is < MinimumTimeoutMinutes or > MaximumInactivityLockTimeoutMinutes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(inactivityLockTimeoutMinutes),
                $"The inactivity lock timeout must be between {MinimumTimeoutMinutes} and {MaximumInactivityLockTimeoutMinutes} minutes.");
        }

        if (sensitiveActionTimeoutMinutes is < MinimumTimeoutMinutes or > MaximumSensitiveActionTimeoutMinutes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sensitiveActionTimeoutMinutes),
                $"The sensitive-action timeout must be between {MinimumTimeoutMinutes} and {MaximumSensitiveActionTimeoutMinutes} minutes.");
        }
    }
}
