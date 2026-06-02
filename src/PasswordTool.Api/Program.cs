using PasswordTool.Api.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var passwordApi = app.MapGroup("/api/password");

passwordApi.MapPost("/hash", (HashPasswordRequest request) =>
{
    return Results.StatusCode(StatusCodes.Status501NotImplemented);
});

passwordApi.MapPost("/verify", (VerifyPasswordRequest request) =>
{
    return Results.StatusCode(StatusCodes.Status501NotImplemented);
});

passwordApi.MapPost("/inspect", (InspectPasswordHashRequest request) =>
{
    return Results.StatusCode(StatusCodes.Status501NotImplemented);
});

app.Run();
