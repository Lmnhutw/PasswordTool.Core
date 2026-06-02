namespace PasswordTool.Api.Contracts;

public sealed record VerifyPasswordRequest(string Password, string StoredHash);
