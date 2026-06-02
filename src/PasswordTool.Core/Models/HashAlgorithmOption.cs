namespace PasswordTool.Core.Models;

public sealed record HashAlgorithmOption
{
    public required string AlgorithmName { get; init; }
    public required string DisplayName { get; init; }
    public required bool IsRecommendedForPasswordStorage { get; init; }
    public required string Description { get; init; }

    public override string ToString()
    {
        return DisplayName;
    }
}
