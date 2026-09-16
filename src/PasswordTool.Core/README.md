# PasswordTool.Core

The reusable domain and security layer for PasswordTool. UI and HTTP projects depend on this project; Core must never depend on WinForms or ASP.NET request/response types.

## Responsibilities

- Master Password validation and PBKDF2-HMAC-SHA256 vault-key derivation
- AES-256-GCM encryption/decryption and encrypted local-vault persistence
- TOTP generation/verification and Windows-DPAPI trusted-unlock tokens
- Optional website TOTP secrets and current-code generation
- Vault item validation, CRUD, recovery-code parsing, and sensitive-action verification
- Encrypted, versioned backup export/import with input limits and conflict classification
- Cryptographically secure password/passphrase generation and strength estimates
- Bounded common-format CSV parsing and duplicate-aware import planning
- Password hash implementations, inspection, registry metadata, and constant-time comparisons

## Storage and sign-in contract

For a new vault, `.config` contains KDF metadata and an AES-GCM-encrypted TOTP secret; `.storage` contains the encrypted vault. A successful Master Password unlock can create `.trusted-unlock`: a one-day Windows-DPAPI-CurrentUser protection of the vault key, bound to a configuration fingerprint. A TOTP code plus that token may unlock the vault only for the same Windows user profile.

File names and Hidden/System attributes are obfuscation only. The security boundary is the Master Password-derived key, authenticated encryption, DPAPI scope, and the Windows user account.

## Change rules

- Do not persist or log raw passwords, recovery codes, Master Passwords, TOTP secrets, or unprotected vault keys.
- Preserve the password-versus-recovery-code invariant on every add, update, import, and export.
- Treat backups as untrusted input: retain schema, size, depth, field-length, version, KDF, and authentication checks before mutating the vault.
- Keep UI and API layers thin. They may choose dialogs, HTTP status codes, and DTOs, but Core owns cryptography and domain validation.
- Maintain backward compatibility for vault items that predate recovery codes: their missing `Type` defaults to `Password`.
- New optional item metadata must keep safe defaults so older encrypted vault and backup payloads continue to deserialize.
