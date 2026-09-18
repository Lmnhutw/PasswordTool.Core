# Architecture and implementation rules

## Purpose and boundaries

PasswordTool is a local Windows vault for passwords, website TOTP secrets, and recovery codes, plus a password-hash utility. It deliberately has no account system, database, cloud sync, telemetry, or vault API.

```text
PasswordTool.WinForms ─┐
                       ├──> PasswordTool.Core <── PasswordTool.Core.Tests
PasswordTool.Api ──────┘
```

`PasswordTool.Core` owns cryptography, validation, and domain workflows. WinForms owns user interaction. The API owns HTTP-specific DTOs and responses. Neither UI nor API may implement encryption, key derivation, TOTP verification, backup parsing, or password-hash algorithms.

## Vault lifecycle

```text
First launch
  choose empty vault or authenticated encrypted-backup recovery
  -> new Master Password + generated TOTP secret + confirmed 6-digit code
    -> Argon2id (3 passes, 64 MiB, parallelism 2, random 32-byte salt)
    -> 256-bit key
    -> encrypt config secret and empty or recovered vault with AES-256-GCM
    -> optionally create a one-day DPAPI-CurrentUser trusted token

Master Password unlock
  derive key -> authenticate/decrypt config and vault -> refresh trusted token

Authenticator unlock
  unexpired same-user DPAPI token -> recover vault key -> verify TOTP -> decrypt vault
```

A Master Password is always the recovery path for an unexpired-token failure. There is no recovery/reset/backdoor if the Master Password, authenticator secret, and usable encrypted backup are lost.
Legacy PBKDF2-HMAC-SHA256 vaults remain readable and are upgraded only after the current Master Password is verified. Changing the Master Password always creates a fresh Argon2id salt and re-encrypts the full vault.

## Persisted data

| File | Purpose | Protection |
| --- | --- | --- |
| `%LocalAppData%\PasswordTool\.config` | KDF metadata, login preference, bounded security timeouts, encrypted TOTP secret, nullable external-backup and verification timestamps | TOTP secret is AES-256-GCM encrypted with the derived vault key. No backup path or passphrase is stored. |
| `%LocalAppData%\PasswordTool\.storage` | Vault items | Entire JSON payload is AES-256-GCM encrypted. |
| `%LocalAppData%\PasswordTool\.trusted-unlock` | Optional one-day trusted-device token | Vault key protected with Windows DPAPI for CurrentUser and tied to a config fingerprint. |
| `%LocalAppData%\PasswordTool\.snapshots` | Up to five prior config/vault pairs | Config and vault remain in their normal encrypted-at-rest formats. |

Hidden/System file attributes are only obfuscation. Treat an incomplete `.config`/`.storage` pair as an error; never silently recreate or overwrite it.
Config and vault writes are one logical state transition: stage both, snapshot the previous complete pair, replace both, and roll back both after a write failure. Snapshot restore also restores a complete pair and removes the trusted-unlock token.

## Vault and backup invariants

- An item has type `Password` or `RecoveryCodes`, never both secret forms.
- A password item may contain one normalized Base32 website TOTP secret. A recovery-code item may not contain password or TOTP data.
- Favorites, folders, and tags live inside the encrypted vault and backup payloads. List clones expose only whether a TOTP secret exists, never the secret itself.
- `Title` is required. Core validates item shape before add/update/export/import.
- TOTP is required to reveal a password or recovery-code list, obtain an item for editing, and export/import backups when a TOTP secret exists.
- A successful application-TOTP check authorizes sensitive actions for the configured 1–30 minute window (five minutes by default). The authorization is in-memory only and is cleared with the vault session or whenever security settings change.
- Backups use the `PasswordToolBackup` version-1 envelope: PBKDF2-SHA256 (600,000 iterations, random 16-byte salt) derives a separate 256-bit key; AES-256-GCM encrypts only vault entries.
- Inspection authenticates and validates the complete backup but returns only format/version, creation time, and item/type/active/Trash counts. It never returns the decrypted payload or secret fields.
- New-machine recovery is allowed only when neither `.config` nor `.storage` exists. Core validates the backup, new Master Password, and new Authenticator confirmation before atomically committing a fresh Argon2id config and recovered encrypted vault. It never imports the old config, vault key, trusted token, or application Authenticator secret.
- Backup-health timestamps change only after an external file write or full authenticated verification succeeds. Config metadata updates use the same paired config/vault state transaction.
- Internal snapshots live beside the vault on the same disk and are not an external or disaster-recovery backup.
- Import is validate-then-commit: enforce 10 MB, JSON depth 32, exact format/KDF/version, authenticated decryption, item limits, and unique IDs; show new/duplicate/conflict items; add new IDs only; rollback in-memory additions when save fails.
- Plaintext CSV import is bounded to 10 MB, 10,000 rows, 64 columns, and bounded fields. It recognizes common browser/manager headers, skips non-login or passwordless rows, previews content, and adds only accounts that do not already match title, username, URL, and password.
- Password and passphrase generation uses `RandomNumberGenerator`; no generated secret is logged or persisted until the user saves the item.
- Password changes keep at most 10 encrypted history entries. Soft-deleted items are excluded from normal queries and are purged after 30 days.
- UpdatedAt is general item metadata. PasswordChangedAt is nullable/version-tolerant lifecycle metadata for active password items only: new/imported passwords and password changes set it from the logical mutation timestamp; non-password edits, Trash, config changes, and key/KDF rewrites do not. Recovery-code conversion clears it and password history. Older payloads resolve an effective date from newest valid history, UpdatedAt, then CreatedAt; dates after the current UTC operation time are invalid and cannot postpone an old-password finding. Unlock normalizes this in memory without saving solely because the vault opened.
- Local Security Check scans active password values in memory only, using exact ordinal reuse comparison and the existing strength estimator. Findings are secret-free and ordered by type, title, then ID. Passwords at least 365 days old (including the exact boundary) are old. WinForms receives findings and returns only a selected item ID; VaultForm performs its existing protected edit and reruns the scan.
- Local Security Check runs only against decrypted in-memory data and returns item metadata plus finding type, never a password value.
- The desktop process enforces one instance and locks after the configured 1–120 minute inactivity window (10 minutes by default).
- WinForms subscribes only while the vault window is open to Windows session-switch and power-mode events. Session lock, console/remote disconnect, suspend, and resume close the vault window; its close path clears decrypted items, the vault key, the Authenticator secret, and sensitive-action authorization before showing the unlock flow again.
- Security timeout and sign-in-mode changes are one Master-Password-authorized config update. Persisted timeout values are validated in Core before use; older configs inherit the secure defaults through version-tolerant property initialization.

## Password hashing

Core exposes the hasher registry, implementations, verifier, and inspector. Argon2id is the default recommendation. bcrypt, PBKDF2-SHA256, PBKDF2-SHA512, scrypt, and ASP.NET Core Identity formats are supported. MD5, SHA-1, and fast SHA variants are educational-only and must never be defaults or be presented as safe password storage.

Use random salts per secure hash and constant-time comparison for verification. Preserve native bcrypt and ASP.NET Core Identity formats; custom secure formats are PHC-style strings.

## API status and deployment rule

`PasswordTool.Api` currently implements `/hash`, `/verify`, `/inspect`, and `/algorithms` below `/api/password`. It does not expose vault operations. HTTPS redirection is configured, but authentication, authorization, rate limiting, request-size limits, audit policy, and an educational-algorithm block are not yet implemented. It must remain local/trusted-development-only until those controls are explicitly added.

## Release qualification boundary

The Windows release path publishes only `PasswordTool.WinForms` plus `PasswordTool.Core`; `PasswordTool.Api` is never a desktop artifact. Phase 5 qualification is implemented as read-only PowerShell validation around the finalized Phase 4 directory. It recalculates hashes, compares the portable ZIP with the publish payload, enforces a public-only manifest, independently verifies any signed claim, and checks the existing offline/build/installer source contracts. It does not add runtime code, networking, storage, or a release service, and it never establishes release readiness when controlled-machine manual scenarios remain incomplete.

## Security rules for future changes

- Never persist, return, or log raw passwords, Master Passwords, recovery codes, TOTP secrets, or unprotected encryption keys.
- Treat all file imports, API inputs, clipboard data, and persisted JSON as untrusted.
- Keep raw secrets out of exceptions, telemetry, diagnostics, and UI list rows.
- Keep the PasswordTool application authenticator secret separate from optional website TOTP secrets stored in entries.
- Clipboard clearing is best-effort risk reduction only. Clear after 30 seconds only when the clipboard still contains the exact value PasswordTool copied.
- Use authenticated encryption and fresh nonces through `EncryptionService`; do not introduce ad-hoc crypto.
- Zero sensitive key buffers where practical and clear vault sessions when closing or on unlock failure.
- Do not represent file hiding, clipboard blocking, or TOTP as protection from malware or a compromised unlocked Windows session.
- Add tests whenever a cryptographic contract, persisted schema, or validation rule changes.
