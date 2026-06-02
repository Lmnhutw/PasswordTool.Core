namespace PasswordTool.Api.Contracts;

public sealed record HashPasswordRequest(string Password, string AlgorithmName);
