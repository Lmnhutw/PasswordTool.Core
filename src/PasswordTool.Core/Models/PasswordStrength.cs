namespace PasswordTool.Core.Models;

public sealed record PasswordStrength(double EstimatedEntropyBits, string Rating);
