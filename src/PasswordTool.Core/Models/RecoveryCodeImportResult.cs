namespace PasswordTool.Core.Models;

public sealed record RecoveryCodeImportResult(IReadOnlyList<string> Codes, string? ErrorMessage)
{
    public bool IsValid => string.IsNullOrEmpty(ErrorMessage);
}
