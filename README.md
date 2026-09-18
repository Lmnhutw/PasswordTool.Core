# PasswordTool

> A local Windows vault for passwords, website TOTP codes, and recovery codes, plus a password-hash utility. Vault data stays on the device; the application has no database, cloud sync, account system, telemetry, or runtime network dependency.

## What is included

| Area | What it does |
| --- | --- |
| **Encrypted vault** | Stores password or recovery-code entries in an AES-256-GCM encrypted local vault. |
| **Sign-in** | Uses a Master Password; Google Authenticator can be used for a limited, trusted-device sign-in path. |
| **Protected actions** | Requires a current TOTP code to reveal secrets or export/import a backup. |
| **Backup & recovery** | Creates and verifies encrypted external backups and can recover a vault on a new Windows installation. |
| **Everyday organization** | Searches locally and organizes entries with favorites, folders, and tags. |
| **Password generation** | Generates cryptographically random passwords and readable passphrases with strength feedback. |
| **Website TOTP** | Stores an optional website TOTP secret inside an encrypted password entry and generates its current code. |
| **Migration** | Reviews and imports common browser or password-manager CSV exports without overwriting matching accounts. |
| **Safety lifecycle** | Keeps password history, a 30-day Trash, paired encrypted snapshots, inactivity lock, and a local weak/reused/old-password check. |
| **Hash utility** | Generates, verifies, and inspects password hashes, including clearly marked educational-only algorithms. |

## Quick start

### Requirements

- Windows 10 or later
- .NET SDK 10 or later
- Git (only when cloning)

Confirm the installed SDK:

```powershell
dotnet --version
```

### Get, restore, and run

Clone the repository, or extract a ZIP so that `PasswordTool.slnx` is in the current folder. Package references are already committed to the project files; do **not** run `dotnet add` to set up the solution.

```powershell
git clone <repository-url>
cd PasswordTool.Core
dotnet restore PasswordTool.slnx
dotnet run --project src\PasswordTool.WinForms\PasswordTool.WinForms.csproj
```

On first launch, create an empty vault or recover one from an encrypted PasswordTool backup. Both paths create a new Master Password (at least 12 characters) and a new PasswordTool Authenticator for this installation. Recovery first authenticates the backup with its separate backup passphrase; cancelling leaves storage uninitialized.

### Verify the solution

```powershell
dotnet test PasswordTool.slnx
dotnet build PasswordTool.slnx
```

## How the vault works

```text
Master Password
  └─ Argon2id (3 passes, 64 MiB, parallelism 2 + random 32-byte salt)
       └─ 256-bit vault key
            ├─ AES-256-GCM encrypts vault items in .storage
            └─ AES-256-GCM encrypts the TOTP secret in .config

Successful Master Password sign-in
  └─ Windows DPAPI (CurrentUser) protects the vault key for one day
       └─ valid TOTP code + unexpired local token can reopen the vault
```

### Sign-in choices

- **Master Password** always derives the vault key and unlocks the vault. A successful sign-in refreshes the one-day trusted token.
- **Google Authenticator** requires both a valid 6-digit TOTP code and an unexpired `.trusted-unlock` token on the *same Windows user profile*. It does not permanently replace the Master Password.
- **Hybrid** is the default preference. The Settings screen can prefer Google Authenticator at the next unlock, but changing this setting requires the Master Password.
- Older vaults without a TOTP secret remain Master-Password-only.
- Existing PBKDF2 vaults remain readable and can be upgraded in Settings; new vaults use Argon2id.
- One successful PasswordTool Authenticator check opens a short sensitive-action session. Settings can choose 1–30 minutes; the default is five. Saving timeout changes clears any active sensitive-action session.
- The vault locks after a configurable 1–120 minutes of system inactivity (10 minutes by default), when the Windows session locks or disconnects, and when Windows suspends or resumes. Only one desktop instance runs per Windows session.

### Vault use and backups

- Entries are either **Password** or **Recovery codes**. A recovery-code entry cannot also contain a password.
- Password entries may also contain a website TOTP secret. This is separate from the PasswordTool Authenticator secret used to protect the vault.
- Favorites, folders, tags, and local search help organize entries without a server or online account.
- URL and Notes may be hidden in the list; the encrypted stored value is unchanged and can be accessed only through the protected edit workflow.
- General copy/cut shortcuts remain disabled. Explicit copy actions for usernames, passwords, and website TOTP codes clear an unchanged clipboard value after 30 seconds. Windows and other applications may read it first.
- Export uses a separate backup passphrase of at least 12 characters. The encrypted envelope contains vault entries only: it excludes the Master Password configuration, TOTP secret, and trusted token.
- The **Backup & Recovery Center** records the last successful external backup and authenticated verification time and warns when no external backup is recorded or the latest is older than 30 days.
- On a new PC, choose **Recover from encrypted backup**, enter the backup passphrase, review safe item counts, then create a new Master Password and Authenticator. Recovery preserves supported item data but deliberately creates a fresh Argon2id salt, trusted token, and application Authenticator secret.
- Import validates the full backup before changing the vault, shows new/duplicate/conflicting IDs, and saves only new entries. Existing entries are never overwritten.
- CSV import supports common headers from browsers and password managers, previews new and duplicate accounts, and adds only new accounts. CSV exports contain plaintext secrets; protect and securely remove them after import.
- Changing a password keeps its latest 10 previous values inside the encrypted vault. Deleted items remain in Trash for 30 days unless restored or permanently deleted.
- UpdatedAt records any item edit; PasswordChangedAt records only when the current password became active. Older vaults derive the latter from the newest valid password-history change, then UpdatedAt, then CreatedAt; impossible future dates are ignored.
- Local Security Check is local-only: it scans active password entries for weak, exactly reused, and passwords at least 365 days old without returning a secret. Its dialog names affected items, supports protected **Edit selected item**, then rescans after the edit.
- Local Security Check reports weak, reused, and passwords unchanged for over one year. It performs no network request and does not expose password values in its result.
- State changes preserve up to five paired `.config` + `.storage` snapshots. Restore always restores the pair and locks the vault so the restored credentials must unlock it again.
- Internal snapshots remain on the same disk. They can undo local changes but are not an external backup and do not protect against disk loss.

## Security model and limits

PasswordTool protects data at rest and provides a local second factor for sensitive actions. It is not a substitute for securing Windows itself.

| Protected by the application | Not protected by the application |
| --- | --- |
| Vault entries and TOTP secret are encrypted with AES-256-GCM. | Malware, a compromised running Windows session, screen capture, or memory inspection while the vault is open. |
| Every new vault uses a random KDF salt; cryptographic key buffers are cleared when sessions end where the runtime permits. | Loss of the Master Password, authenticator secret, and usable backups: there is no recovery, reset, backdoor, or cloud copy. |
| The trusted token is DPAPI-protected for the current Windows user and bound to the current vault configuration. | Another user/profile or device using that token; DPAPI protection is scoped to the local Windows user, not portable. |
| TOTP gates secret reveal, editing, and backup export/import when configured. | A weak Master Password or an unlocked device left accessible to another person. |
| Sensitive clipboard values are cleared after 30 seconds when unchanged. | Another process reading the clipboard, clipboard history, remote-control software, or malware. |

Keep Windows patched, use a strong unique Master Password, lock the PC when away, protect the authenticator and backup passphrase separately, and keep encrypted backups in a location you control. Hidden/System file attributes are only concealment; encryption and Windows account security are the actual boundaries.

## Local files

The desktop app stores its files in:

```text
%LocalAppData%\PasswordTool
```

| File | Contents |
| --- | --- |
| `.config` | KDF metadata, login preference, validated security timeouts, encrypted TOTP secret, and nullable backup-health timestamps. |
| `.storage` | AES-256-GCM encrypted vault payload. |
| `.trusted-unlock` | Optional, one-day DPAPI-protected vault key for the current Windows user. |
| `.snapshots` | Up to five previous paired config/vault states, retaining the same encrypted-at-rest representation. |

Do not manually edit, mix, or partially restore these files. If only `.config` or `.storage` is present, the app stops rather than overwriting partial storage.

## Password hashing utility and API

The desktop **Hash Tool** and the optional API share `PasswordTool.Core` implementations. Argon2id is the default recommendation. bcrypt, PBKDF2, scrypt, and ASP.NET Core Identity formats are supported; MD5/SHA family educational options are intentionally labeled unsafe and must never be used for production password storage.

The API currently exposes local development endpoints:

- `POST /api/password/hash`
- `POST /api/password/verify`
- `POST /api/password/inspect`
- `GET /api/password/algorithms`

It has no authentication, rate limiting, or production hardening today. Do not expose it to an untrusted network. See [the API README](src/PasswordTool.Api/README.md) before running it.

## Publish a single-file EXE

```powershell
dotnet publish src\PasswordTool.WinForms\PasswordTool.WinForms.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Output:

```text
src\PasswordTool.WinForms\bin\Release\net10.0-windows\win-x64\publish\PasswordTool.WinForms.exe
```

## Repository layout

```text
src/
  PasswordTool.Core/       # cryptography, vault workflows, hash implementations
  PasswordTool.WinForms/   # local Windows interface
  PasswordTool.Api/        # optional Minimal API for hash operations
tests/
  PasswordTool.Core.Tests/ # core behavior and security-rule tests
docs/
  architecture.md          # boundaries, data flows, and developer rules
```

For implementation details and rules for future changes, read [docs/architecture.md](docs/architecture.md).
