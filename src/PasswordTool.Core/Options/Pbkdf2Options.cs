namespace PasswordTool.Core.Options;

public sealed record Pbkdf2Options
{
    public int Iterations { get; init; } = 210000;
    public int SaltSizeBytes { get; init; } = 16;
    public int HashSizeBytes { get; init; } = 32;
}
