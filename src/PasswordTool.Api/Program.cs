using PasswordTool.Api.Contracts;
using PasswordTool.Core.Abstractions;
using PasswordTool.Core.Hashers;
using PasswordTool.Core.Inspection;
using PasswordTool.Core.Registry;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSingleton<IPasswordHasherRegistry, PasswordHasherRegistry>();
builder.Services.AddSingleton<IPasswordHashInspector, PasswordHashInspector>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var passwordApi = app.MapGroup("/api/password");

passwordApi.MapPost("/hash", (HashPasswordRequest request, IPasswordHasherRegistry registry) =>
{
    if (string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new { error = "Password is required." });
    }

    if (string.IsNullOrWhiteSpace(request.AlgorithmName))
    {
        return Results.BadRequest(new { error = "AlgorithmName is required." });
    }

    try
    {
        var hasher = registry.GetHasher(request.AlgorithmName);
        var storedHash = hasher.HashPassword(request.Password);

        return Results.Ok(new HashPasswordResponse(hasher.AlgorithmName, storedHash));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

passwordApi.MapPost("/verify", (VerifyPasswordRequest request, IPasswordHasherRegistry registry) =>
{
    if (string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new { error = "Password is required." });
    }

    if (string.IsNullOrWhiteSpace(request.StoredHash))
    {
        return Results.BadRequest(new { error = "StoredHash is required." });
    }

    foreach (var descriptor in registry.GetAvailableHashers())
    {
        var hasher = registry.GetHasher(descriptor.AlgorithmName);
        if (hasher.VerifyPassword(request.Password, request.StoredHash))
        {
            return Results.Ok(new VerifyPasswordResponse(true));
        }
    }

    return Results.Ok(new VerifyPasswordResponse(false));
});

passwordApi.MapPost("/inspect", (InspectPasswordHashRequest request, IPasswordHashInspector inspector) =>
{
    if (string.IsNullOrWhiteSpace(request.StoredHash))
    {
        return Results.BadRequest(new { error = "StoredHash is required." });
    }

    return Results.Ok(inspector.Inspect(request.StoredHash));
});

passwordApi.MapGet("/algorithms", (IPasswordHasherRegistry registry) =>
{
    return Results.Ok(registry.GetAvailableHashers());
});

app.Run();
