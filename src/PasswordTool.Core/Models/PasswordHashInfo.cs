namespace PasswordTool.Core.Models;

public sealed record PasswordHashInfo
{
    public string AlgorithmName { get; init; } = string.Empty;
    public int? Version { get; init; }
    public string? Salt { get; init; }
    public string? Hash { get; init; }
    public int? Iterations { get; init; }
    public int? WorkFactor { get; init; }
    public int? MemoryCost { get; init; }
    public int? Parallelism { get; init; }
    public bool IsSecureForPasswordStorage { get; init; }
    public string Notes { get; init; } = string.Empty;
}
