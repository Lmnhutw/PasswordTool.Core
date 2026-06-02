namespace PasswordTool.Core.Options;

public sealed record Argon2idOptions
{
    public int MemoryCost { get; init; } = 65536;
    public int Iterations { get; init; } = 3;
    public int Parallelism { get; init; } = 2;
    public int SaltSizeBytes { get; init; } = 16;
    public int HashSizeBytes { get; init; } = 32;
}
