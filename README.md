# PasswordTool

> A local Windows password vault and password-hash utility. Vault data stays on the device; the application has no database, cloud sync, account system, or network dependency after NuGet packages are restored.

## What is included

| Area | What it does |
| --- | --- |
| **Encrypted vault** | Stores password or recovery-code entries in an AES-256-GCM encrypted local vault. |
| **Sign-in** | Uses a Master Password; Google Authenticator can be used for a limited, trusted-device sign-in path. |
| **Protected actions** | Requires a current TOTP code to reveal secrets or export/import a backup. |
| **Encrypted backup** | Exports a versioned JSON envelope encrypted with a separate backup passphrase. |
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

On the first launch, choose a Master Password (at least 12 characters), scan the QR code in Google Authenticator or another compatible TOTP app, and confirm a 6-digit code. Only then does PasswordTool create the empty vault.

### Verify the solution

```powershell
dotnet test PasswordTool.slnx
dotnet build PasswordTool.slnx
```

## How the vault works

```text
Master Password
  └─ PBKDF2-HMAC-SHA256 (600,000 iterations + random 32-byte salt)
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

### Vault use and backups

- Entries are either **Password** or **Recovery codes**. A recovery-code entry cannot also contain a password.
- URL and Notes may be hidden in the list; the encrypted stored value is unchanged and can be accessed only through the protected edit workflow.
- The app disables copy/cut shortcuts and copy buttons within its UI. This reduces accidental clipboard exposure, but cannot erase data that has already been pasted from or copied by another application.
- Export uses a separate backup passphrase of at least 12 characters. The encrypted envelope contains vault entries only: it excludes the Master Password configuration, TOTP secret, and trusted token.
- Import validates the full backup before changing the vault, shows new/duplicate/conflicting IDs, and saves only new entries. Existing entries are never overwritten.

## Security model and limits

PasswordTool protects data at rest and provides a local second factor for sensitive actions. It is not a substitute for securing Windows itself.

| Protected by the application | Not protected by the application |
| --- | --- |
| Vault entries and TOTP secret are encrypted with AES-256-GCM. | Malware, a compromised running Windows session, screen capture, or memory inspection while the vault is open. |
| Every new vault uses a random KDF salt; cryptographic key buffers are cleared when sessions end where the runtime permits. | Loss of the Master Password, authenticator secret, and usable backups: there is no recovery, reset, backdoor, or cloud copy. |
| The trusted token is DPAPI-protected for the current Windows user and bound to the current vault configuration. | Another user/profile or device using that token; DPAPI protection is scoped to the local Windows user, not portable. |
| TOTP gates secret reveal, editing, and backup export/import when configured. | A weak Master Password or an unlocked device left accessible to another person. |

Keep Windows patched, use a strong unique Master Password, lock the PC when away, protect the authenticator and backup passphrase separately, and keep encrypted backups in a location you control. Hidden/System file attributes are only concealment; encryption and Windows account security are the actual boundaries.

## Local files

The desktop app stores its files in:

```text
%LocalAppData%\PasswordTool
```

| File | Contents |
| --- | --- |
| `.config` | KDF metadata, login preference, and the encrypted TOTP secret. |
| `.storage` | AES-256-GCM encrypted vault payload. |
| `.trusted-unlock` | Optional, one-day DPAPI-protected vault key for the current Windows user. |

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
