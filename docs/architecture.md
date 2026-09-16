# Architecture and implementation rules

## Purpose and boundaries

PasswordTool is a local Windows password vault plus a password-hash utility. It deliberately has no account system, database, cloud sync, or vault API.

```text
PasswordTool.WinForms ─┐
                       ├──> PasswordTool.Core <── PasswordTool.Core.Tests
PasswordTool.Api ──────┘
```

`PasswordTool.Core` owns cryptography, validation, and domain workflows. WinForms owns user interaction. The API owns HTTP-specific DTOs and responses. Neither UI nor API may implement encryption, key derivation, TOTP verification, backup parsing, or password-hash algorithms.

## Vault lifecycle

```text
First launch
  Master Password + generated TOTP secret + confirmed 6-digit code
    -> PBKDF2-HMAC-SHA256 (600,000 iterations, random 32-byte salt)
    -> 256-bit key
    -> encrypt config secret and empty vault with AES-256-GCM
    -> optionally create a one-day DPAPI-CurrentUser trusted token

Master Password unlock
  derive key -> authenticate/decrypt config and vault -> refresh trusted token

Authenticator unlock
  unexpired same-user DPAPI token -> recover vault key -> verify TOTP -> decrypt vault
```

A Master Password is always the recovery path for an unexpired-token failure. There is no recovery/reset/backdoor if the Master Password, authenticator secret, and usable encrypted backup are lost.

## Persisted data

| File | Purpose | Protection |
| --- | --- | --- |
| `%LocalAppData%\PasswordTool\.config` | KDF metadata, login preference, encrypted TOTP secret | TOTP secret is AES-256-GCM encrypted with the derived vault key. |
| `%LocalAppData%\PasswordTool\.storage` | Vault items | Entire JSON payload is AES-256-GCM encrypted. |
| `%LocalAppData%\PasswordTool\.trusted-unlock` | Optional one-day trusted-device token | Vault key protected with Windows DPAPI for CurrentUser and tied to a config fingerprint. |

Hidden/System file attributes are only obfuscation. Treat an incomplete `.config`/`.storage` pair as an error; never silently recreate or overwrite it.

## Vault and backup invariants

- An item has type `Password` or `RecoveryCodes`, never both secret forms.
- `Title` is required. Core validates item shape before add/update/export/import.
- TOTP is required to reveal a password or recovery-code list, obtain an item for editing, and export/import backups when a TOTP secret exists.
- Backups use the `PasswordToolBackup` version-1 envelope: PBKDF2-SHA256 (600,000 iterations, random 16-byte salt) derives a separate 256-bit key; AES-256-GCM encrypts only vault entries.
- Import is validate-then-commit: enforce 10 MB, JSON depth 32, exact format/KDF/version, authenticated decryption, item limits, and unique IDs; show new/duplicate/conflict items; add new IDs only; rollback in-memory additions when save fails.

## Password hashing

Core exposes the hasher registry, implementations, verifier, and inspector. Argon2id is the default recommendation. bcrypt, PBKDF2-SHA256, PBKDF2-SHA512, scrypt, and ASP.NET Core Identity formats are supported. MD5, SHA-1, and fast SHA variants are educational-only and must never be defaults or be presented as safe password storage.

Use random salts per secure hash and constant-time comparison for verification. Preserve native bcrypt and ASP.NET Core Identity formats; custom secure formats are PHC-style strings.

## API status and deployment rule

`PasswordTool.Api` currently implements `/hash`, `/verify`, `/inspect`, and `/algorithms` below `/api/password`. It does not expose vault operations. HTTPS redirection is configured, but authentication, authorization, rate limiting, request-size limits, audit policy, and an educational-algorithm block are not yet implemented. It must remain local/trusted-development-only until those controls are explicitly added.

## Security rules for future changes

- Never persist, return, or log raw passwords, Master Passwords, recovery codes, TOTP secrets, or unprotected encryption keys.
- Treat all file imports, API inputs, clipboard data, and persisted JSON as untrusted.
- Keep raw secrets out of exceptions, telemetry, diagnostics, and UI list rows.
- Use authenticated encryption and fresh nonces through `EncryptionService`; do not introduce ad-hoc crypto.
- Zero sensitive key buffers where practical and clear vault sessions when closing or on unlock failure.
- Do not represent file hiding, clipboard blocking, or TOTP as protection from malware or a compromised unlocked Windows session.
- Add tests whenever a cryptographic contract, persisted schema, or validation rule changes.