# PasswordTool.Core

The core owns Argon2id/PBKDF2 key derivation, AES-GCM vault persistence, paired snapshots, encrypted-backup inspection and new-machine recovery, bounded security timers, Master Password and Authenticator rotation, password history, Trash retention, and local security findings. UI and API layers must call these workflows rather than reproduce cryptographic or persistence logic.

The reusable domain and security layer for PasswordTool. UI and HTTP projects depend on this project; Core must never depend on WinForms or ASP.NET request/response types.

## Responsibilities

- Master Password validation, current Argon2id vault-key derivation, and legacy PBKDF2 compatibility
- AES-256-GCM encryption/decryption and encrypted local-vault persistence
- TOTP generation/verification and Windows-DPAPI trusted-unlock tokens
- Master-Password-authorized, range-validated inactivity and sensitive-action timeout settings
- Optional website TOTP secrets and current-code generation
- Vault item validation, CRUD, recovery-code parsing, and sensitive-action verification
- Encrypted, versioned backup creation, safe authenticated inspection, import planning, and atomic new-machine recovery
- Cryptographically secure password/passphrase generation and strength estimates
- Bounded common-format CSV parsing and duplicate-aware import planning
- Password hash implementations, inspection, registry metadata, and constant-time comparisons
- Password lifecycle metadata and local, secret-free weak/reused/old Security Check analysis

## Storage and sign-in contract

For a new vault, `.config` contains KDF metadata and an AES-GCM-encrypted TOTP secret; `.storage` contains the encrypted vault. A successful Master Password unlock can create `.trusted-unlock`: a one-day Windows-DPAPI-CurrentUser protection of the vault key, bound to a configuration fingerprint. A TOTP code plus that token may unlock the vault only for the same Windows user profile.

Recovery accepts only completely uninitialized storage. It validates the encrypted backup and all new credentials before a single paired state commit, then creates the trusted token. The recovered config uses a fresh Argon2id salt and new Authenticator secret. Nullable backup-health timestamps remain compatible with older config files; no destination path or backup passphrase is persisted.

File names and Hidden/System attributes are obfuscation only. The security boundary is the Master Password-derived key, authenticated encryption, DPAPI scope, and the Windows user account.

## Change rules

- Do not persist or log raw passwords, recovery codes, Master Passwords, TOTP secrets, or unprotected vault keys.
- Preserve the password-versus-recovery-code invariant on every add, update, import, and export.
- Treat backups as untrusted input: retain schema, size, depth, field-length, version, KDF, and authentication checks before mutating the vault.
- Keep inspection results metadata-only; never return decrypted backup payloads to a UI.
- Keep UI and API layers thin. They may choose dialogs, HTTP status codes, and DTOs, but Core owns cryptography and domain validation.
- Maintain backward compatibility for vault items that predate recovery codes: their missing `Type` defaults to `Password`.
- New optional item metadata must keep safe defaults so older encrypted vault and backup payloads continue to deserialize.
- UpdatedAt is not password age. PasswordChangedAt is nullable for legacy payloads and is resolved in Core from valid history, UpdatedAt, then CreatedAt; future dates are ignored. The 365-day Security Check threshold is inclusive and findings never contain a secret.
- Older configs without timeout fields default to a 10-minute inactivity lock and five-minute sensitive-action session. Never accept persisted or requested values outside Core's supported ranges.
