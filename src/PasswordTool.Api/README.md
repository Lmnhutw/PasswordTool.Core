# PasswordTool.Api

Optional ASP.NET Core Minimal API over the password-hashing capabilities in `PasswordTool.Core`. It does **not** expose the encrypted vault.

## Current endpoints

| Method | Route | Purpose |
| --- | --- | --- |
| `POST` | `/api/password/hash` | Hashes `Password` using `AlgorithmName`. |
| `POST` | `/api/password/verify` | Checks `Password` against `StoredHash`. |
| `POST` | `/api/password/inspect` | Parses supported metadata from `StoredHash`. |
| `GET` | `/api/password/algorithms` | Lists supported algorithms and their security category. |

Run locally:

```powershell
dotnet run --project src\PasswordTool.Api\PasswordTool.Api.csproj --launch-profile https
```

During development, OpenAPI is available at the HTTPS development address. `PasswordTool.Api.http` contains example requests.

## Security status

This project is a local-development boundary, not a ready-to-deploy public service. It currently has HTTPS redirection but does **not** configure authentication, authorization, rate limiting, request-size limits, audit controls, or production secret handling. Its hash and verify routes receive raw passwords in memory.

Before exposing it beyond a trusted local development environment, add authentication and authorization, HTTPS certificate and proxy configuration, rate limiting and request limits, safe observability that excludes passwords/hashes, and an explicit policy that blocks educational-only algorithms. Keep the Core implementation as the sole source of hashing behavior; do not duplicate cryptography in handlers.
