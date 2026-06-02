# Architecture

## Goal

Build a local password hashing tool where the hashing logic is separated from UI and API layers.

## Dependency Direction

```text
PasswordTool.WinForms -> PasswordTool.Core
PasswordTool.Api      -> PasswordTool.Core
PasswordTool.Core.Tests -> PasswordTool.Core
```

`PasswordTool.Core` must not reference WinForms or ASP.NET Core UI/API concerns.

## Project Responsibilities

### PasswordTool.Core

Owns:

- password hashing interfaces
- algorithm option records
- algorithm implementations
- stored hash parsing and inspection
- constant-time comparison helpers
- algorithm metadata and registry

Does not own:

- WinForms controls
- HTTP request/response handling
- logging raw passwords
- storing plain-text passwords

### PasswordTool.WinForms

Owns:

- password input UI
- algorithm selector
- hash output display
- verify password workflow
- copy hash action
- show/hide password toggle
- hash inspector/debug panel
- warning labels for educational-only algorithms

Does not own hashing logic. It should call `IPasswordHasherRegistry`, `IPasswordHasher`, and `IPasswordHashInspector` from `PasswordTool.Core`.

### PasswordTool.Api

Owns:

- HTTP routes
- request validation
- response DTOs
- API-specific error handling
- authentication, rate limiting, and HTTPS policy when exposed beyond local development

Does not own hashing logic. It should call `PasswordTool.Core`.

## Core Interfaces

```csharp
public interface IPasswordHasher
{
    string AlgorithmName { get; }
    string HashPassword(string password);
    bool VerifyPassword(string password, string storedHash);
}

public interface IPasswordHashInspector
{
    PasswordHashInfo Inspect(string storedHash);
}

public interface IPasswordHasherRegistry
{
    IReadOnlyList<PasswordHasherDescriptor> GetAvailableHashers();
    IPasswordHasher GetHasher(string algorithmName);
}
```

## Models

`PasswordHashInfo` describes parsed hash components:

- algorithm name
- version
- salt
- hash
- iterations
- work factor
- memory cost
- parallelism
- whether the algorithm is secure for password storage
- notes and warnings

`PasswordHasherDescriptor` describes an available algorithm for UI/API selection.

## Algorithm List

Production-safe:

- Argon2id
- bcrypt
- PBKDF2-HMAC-SHA256
- PBKDF2-HMAC-SHA512
- scrypt

Framework format:

- ASP.NET Core Identity PasswordHasher format

Educational only:

- MD5
- SHA1
- SHA256 without salt
- SHA512 without salt
- SHA256 with salt
- SHA512 with salt

Educational-only algorithms must never be defaults and must be clearly labeled as unsafe for real password storage.

## Stored Hash Format

Custom hashers should use readable PHC-style strings:

```text
$argon2id$v=1$m=65536,t=3,p=2$saltBase64$hashBase64
$pbkdf2-sha256$v=1$i=600000$saltBase64$hashBase64
$pbkdf2-sha512$v=1$i=600000$saltBase64$hashBase64
$scrypt$v=1$n=16384,r=8,p=1$saltBase64$hashBase64
```

bcrypt and ASP.NET Core Identity should preserve their native stored hash formats.

## Implementation Order

1. Implement PBKDF2-HMAC-SHA256 first using built-in .NET APIs.
2. Add `PasswordHashInspector` parsing for PBKDF2.
3. Build the WinForms controls around the core interfaces.
4. Add bcrypt, Argon2id, and scrypt using vetted libraries.
5. Add educational-only hashers with clear warnings.
6. Add API endpoint implementations only after core behavior is tested.

## Security Rules

- Never store plain-text passwords.
- Never log raw passwords.
- Never return raw passwords in API responses.
- Use a random salt for every secure password hash.
- Use constant-time comparison for verification.
- Do not select MD5, SHA1, SHA256, or SHA512 as production password storage algorithms.
- Treat a public password hashing API as sensitive infrastructure requiring HTTPS, authentication, rate limiting, and abuse protection.
