namespace PasswordTool.Core.Options;

public sealed record BcryptOptions
{
    public int WorkFactor { get; init; } = 12;
}
