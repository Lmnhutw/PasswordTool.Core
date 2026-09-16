namespace PasswordTool.Core.Models;

public sealed class PasswordGenerationOptions
{
    public int Length { get; init; } = 20;

    public bool IncludeUppercase { get; init; } = true;

    public bool IncludeLowercase { get; init; } = true;

    public bool IncludeDigits { get; init; } = true;

    public bool IncludeSymbols { get; init; } = true;
}
