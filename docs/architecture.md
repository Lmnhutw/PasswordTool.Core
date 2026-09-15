# Architecture

## Goal

Build a local encrypted password vault and password hashing tool where security and business logic are separated from UI and API layers.

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
- encrypted vault item invariants and CRUD workflows
- recovery-code parsing and validation
- encrypted JSON backup serialization, validation, and import conflict classification

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
- clipboard-triggered recovery-code review UI
- JSON backup file selection, passphrase prompts, and import review UI

Does not own hashing logic. It should call `IPasswordHasherRegistry`, `IPasswordHasher`, and `IPasswordHashInspector` from `PasswordTool.Core`.

WinForms also does not own vault-item validation, backup cryptography, backup schema parsing, or import conflict rules. It delegates those operations to `VaultService` and `VaultBackupService` through the Core boundary.

## Vault Item Compatibility

`VaultItem.Type` defaults to `Password`, so existing encrypted vault JSON that predates recovery-code support deserializes without migration or data loss. A password item may contain a password but no recovery-code list. A recovery-code item may contain recovery codes but no password. Core validates this invariant before add, update, export, or import.

## JSON Backup Boundary

The exported file is JSON, but vault items remain encrypted. The envelope contains a format/version marker, fixed PBKDF2-HMAC-SHA256 metadata, a random salt, and an AES-256-GCM encrypted payload. The payload excludes `.config`, the encrypted TOTP secret, and `.trusted-unlock`.

Import follows a validate-then-commit workflow:

1. Enforce the file-size and JSON-depth limits.
2. Validate the exact backup format, version, KDF parameters, and salt size before key derivation.
3. Authenticate and decrypt the AES-GCM payload.
4. Validate every item, unique ID, field length, item type, and recovery-code list.
5. Classify IDs as new, duplicate, or conflict for UI review.
6. Add only new IDs and save the encrypted vault once; roll back the in-memory additions if persistence fails.

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
