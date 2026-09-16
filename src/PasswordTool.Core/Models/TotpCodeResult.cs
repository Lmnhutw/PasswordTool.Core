namespace PasswordTool.Core.Models;

public sealed record TotpCodeResult(string Code, int SecondsRemaining);
