namespace PasswordTool.Api.Contracts;

public sealed record HashPasswordResponse(string AlgorithmName, string StoredHash);
