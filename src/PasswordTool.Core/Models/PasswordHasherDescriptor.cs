namespace PasswordTool.Core.Models;

public sealed record PasswordHasherDescriptor
{
    public required string AlgorithmName { get; init; }
    public required PasswordHasherSecurityCategory SecurityCategory { get; init; }
    public required bool IsDefaultRecommendation { get; init; }
    public required string Description { get; init; }
}
