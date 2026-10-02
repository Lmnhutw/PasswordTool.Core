namespace PasswordTool.Core.Models;

public sealed record VaultSecuritySettings(
    int InactivityLockTimeoutMinutes,
    int SensitiveActionTimeoutMinutes,
    int VaultOpenDurationMinutes = 1)
{
    public const int DefaultInactivityLockTimeoutMinutes = 1;
    public const int DefaultSensitiveActionTimeoutMinutes = 5;
    public const int DefaultVaultOpenDurationMinutes = 1;
    public const int MaximumSessionDurationMinutes = 300;
    public const int MinimumTimeoutMinutes = 1;
    public const int MaximumInactivityLockTimeoutMinutes = 120;
    public const int MaximumSensitiveActionTimeoutMinutes = 30;

    public static void Validate(int inactivityLockTimeoutMinutes, int sensitiveActionTimeoutMinutes, int vaultOpenDurationMinutes = DefaultVaultOpenDurationMinutes)
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

        if (vaultOpenDurationMinutes is not (1 or 2 or 5 or 10 or 30 or 60 or 120 or 300))
            throw new ArgumentOutOfRangeException(nameof(vaultOpenDurationMinutes), "The vault open duration must be 1, 2, 5, 10, 30, 60, 120, or 300 minutes.");
    }
}
