namespace PasswordTool.Core.Options;

public sealed record ScryptOptions
{
    public int Cost { get; init; } = 16384;
    public int BlockSize { get; init; } = 8;
    public int Parallelization { get; init; } = 1;
    public int SaltSizeBytes { get; init; } = 16;
    public int HashSizeBytes { get; init; } = 32;
}
