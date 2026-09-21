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

## Download and install

The recommended way to use PasswordTool is to download an approved Windows x64 release from the [GitHub Releases page](https://github.com/Lmnhutw/PasswordTool.Core/releases). A release may provide either or both of these packages:

- **Installer (`.exe`)** — the easiest option. It installs PasswordTool for the current Windows user, adds a Start Menu shortcut, and does not require administrator access.
- **Portable ZIP** — extract the complete ZIP to a folder you control, then run `PasswordTool.WinForms.exe`. Do not run the executable from inside the ZIP or copy only the `.exe` out of its folder.

Before running a downloaded build:

1. Read `release-status.txt`. Use a `SIGNED` release for normal use. `UNSIGNED` means developer/test evaluation only; Windows may show an unknown-publisher warning.
2. Compare the package SHA-256 value with the matching entry in `checksums.sha256`:

   ```powershell
   Get-FileHash .\PasswordTool-<version>-win-x64.zip -Algorithm SHA256
   ```

3. For a signed executable, open **Properties → Digital Signatures** and confirm the signature is valid, or verify it with `signtool verify /pa /tw <file>` when Windows SDK tools are available.

PasswordTool releases are self-contained: an end user does not need to install .NET separately. The application runs locally and does not require an account, browser extension, cloud service, or internet connection. New versions are installed manually; PasswordTool has no auto-updater.

### Upgrade, uninstall, and portable use

- Installing a newer approved installer over an older one keeps the vault in `%LocalAppData%\PasswordTool`.
- Uninstalling removes application binaries but intentionally keeps the vault. Delete the vault directory manually only when you are certain you have a usable encrypted backup and intend to erase local data.
- A portable executable is portable; the vault is not. By default, it still uses `%LocalAppData%\PasswordTool` on the current Windows profile.
- Never copy only `.config` or only `.storage` between machines. Use **Backup & Recovery Center** and an encrypted backup instead.

## First-time setup

1. Start PasswordTool and choose **Create a new vault**. If you already have a PasswordTool encrypted backup, choose **Recover from encrypted backup** instead.
2. Create a strong, unique Master Password of at least 12 characters. A long passphrase that you do not reuse elsewhere is recommended.
3. Scan the displayed QR code with Google Authenticator or another compatible TOTP application.
4. Enter the current 6-digit code to confirm setup.
5. Store the Master Password safely and make sure the Authenticator entry is backed up according to your authenticator application's recovery/export process. Keep those recovery paths separate. PasswordTool has no server-side reset, recovery email, administrator override, or backdoor.
6. Add a test item, lock the vault, unlock it again, and create an encrypted external backup before relying on the vault for important data.

The Master Password is always the durable way to unlock the vault. Google Authenticator sign-in is a convenience path for the same trusted Windows user and depends on a valid local token. It can expire or become invalid after configuration changes, Windows-profile changes, restoring files, or moving to another computer; in those cases, sign in with the Master Password.

## Everyday use

1. **Unlock:** enter the Master Password, or use the current PasswordTool Authenticator code when the trusted-device option is available.
2. **Add and organize:** create Password or Recovery-code entries and optionally assign favorites, folders, tags, URLs, notes, or a website-specific TOTP secret.
3. **Reveal or copy a secret:** enter the current PasswordTool Authenticator code when prompted. PasswordTool decrypts the vault in the active application session and shows only the requested value; the code itself does not decrypt the password.
4. **Lock:** use the lock action when finished. The app also locks on configured inactivity, Windows lock/disconnect, suspend, and resume boundaries.
5. **Back up:** regularly export an encrypted backup using a separate strong backup passphrase, store it away from the PC, and verify it in **Backup & Recovery Center**.
6. **Check safety:** run **Local Security Check** to find weak, exactly reused, or old passwords without sending values to an online service.

When recovering on another Windows installation, the backup passphrase decrypts the backup. You then create a new Master Password and a new PasswordTool Authenticator for that installation. The old trusted-device token is deliberately not transferred.

### What must be kept safe

| If this is lost | Result |
| --- | --- |
| Master Password | Google Authenticator sign-in may work only while the local one-day trusted token remains valid. It cannot reset or reveal the Master Password. Create an encrypted backup before losing access. |
| Authenticator entry/device | Use the Master Password to unlock, then reset the PasswordTool Authenticator in Settings. |
| Both Master Password and usable trusted Authenticator access | The local vault cannot be recovered. There is deliberately no backdoor. An encrypted backup is useful only if its separate backup passphrase is known. |
| Backup passphrase | That backup cannot be decrypted. The live vault is unaffected while its own Master Password remains available. |
| Computer or Windows profile | Restore an encrypted external backup on the new installation. Copying the trusted token does not make it portable because DPAPI binds it to the Windows user context. |

## Build from source

### Requirements

- Windows 10 or later
- .NET SDK 10 or later
- Git (only when cloning)

Confirm the installed SDK:

```powershell
dotnet --version
```

### Clone, restore, and run

Clone the repository, or extract a ZIP so that `PasswordTool.slnx` is in the current folder. Package references are already committed to the project files; do **not** run `dotnet add` to set up the solution.

```powershell
git clone <repository-url>
cd PasswordTool.Core
dotnet restore PasswordTool.slnx
dotnet run --project src\PasswordTool.WinForms\PasswordTool.WinForms.csproj
```

Building from source is intended for developers. It does not establish that a local build is an approved, signed release. On first launch, follow the setup flow above.

### Verify the solution

```powershell
dotnet test PasswordTool.slnx
dotnet build PasswordTool.slnx
```

## How encryption and unlocking work

PasswordTool does not store the Master Password and does not merely hide password text behind the Google Authenticator screen. The durable files contain authenticated ciphertext. To display a saved password, the application must obtain the vault encryption key, authenticate and decrypt the vault, then authorize the reveal action.

```text
Random 256-bit DEK
  ├─ AES-256-GCM encrypts vault items in .storage
  └─ AES-256-GCM encrypts the PasswordTool Authenticator secret in .config

Master Password
  └─ Argon2id (3 passes, 64 MiB, parallelism 2 + random 32-byte salt)
       └─ KEK wraps the DEK with AES-256-GCM

Successful Master Password sign-in
  └─ Windows DPAPI (CurrentUser) separately protects the Authenticator secret and DEK for one day
       └─ the app verifies TOTP before asking DPAPI to release the DEK
```

The terms in that flow mean:

- **DEK (Data Encryption Key):** a random 256-bit key created independently for each new vault. It encrypts `.storage` and the PasswordTool Authenticator secret with AES-256-GCM.
- **KEK (Key Encryption Key):** a key derived from the Master Password and a random salt using Argon2id. It is not stored. It wraps, or encrypts, the DEK.
- **Envelope encryption:** vault data is encrypted with the random DEK, while the Master Password-derived KEK encrypts only that DEK. Therefore, changing the Master Password or strengthening KDF parameters re-wraps the same DEK without decrypting and re-encrypting the entire vault file.
- **AES-256-GCM:** authenticated encryption. A wrong key, modified ciphertext, or ciphertext used in the wrong context fails authentication instead of returning unchecked plaintext. PasswordTool binds the wrapped DEK, vault payload, and Authenticator secret to different purposes.
- **DPAPI CurrentUser:** a Windows protection boundary used only for the optional, short-lived trusted-device token. Its protected blobs can be opened only in the same Windows user context under normal platform operation.

### What happens when a password is shown

1. The encrypted vault remains in `.storage`; the UI masking characters are not the security boundary.
2. The vault must already be unlocked. A Master Password unlock derives the KEK and unwraps the DEK. A trusted-device unlock first validates the current TOTP code, then asks DPAPI to release its protected copy of the DEK.
3. PasswordTool uses the DEK to authenticate and decrypt the vault into the running process's session memory.
4. Revealing, editing, or exporting sensitive data requires a current PasswordTool Authenticator code when configured. This code authorizes the action; it is not converted into the encryption key and does not decrypt the password by itself.
5. Only after that check does the UI display or explicitly copy the requested value. Copying exposes the value to the Windows clipboard temporarily; PasswordTool clears it after 30 seconds if it has not changed.
6. Locking or ending the session clears application-held key material where the runtime permits. The on-disk vault remains encrypted throughout.

This separation is why possession of a current 6-digit code alone is insufficient to open a copied vault on another computer: the attacker would still need the Master Password, or a usable trusted token protected for the original Windows user. Conversely, TOTP is not protection against malware already controlling that same unlocked Windows account or reading the process while the vault is open.

### Vault-format migration

Vault format v3 introduced envelope encryption. After a successful Master Password unlock of a supported v1/v2 vault, PasswordTool creates a fresh random DEK, stages the v3 config and vault, reads and cryptographically verifies both staged files, preserves a paired legacy snapshot, and only then replaces the active pair. If any stage fails, the original pair is restored and the current legacy session may continue with a migration-deferred warning. Google Authenticator trusted unlock remains unavailable until migration succeeds.

### Sign-in choices

- **Master Password** derives a KEK that unwraps the random vault DEK. A successful sign-in refreshes the one-day trusted token.
- **Google Authenticator** requires both a valid 6-digit TOTP code and an unexpired `.trusted-unlock` token on the *same Windows user profile*. It does not permanently replace the Master Password.
- **Hybrid** is the default preference. The Settings screen can prefer Google Authenticator at the next unlock, but changing this setting requires the Master Password.
- Older vaults without a TOTP secret remain Master-Password-only.
- Existing v1/v2 vaults remain readable and migrate atomically to v3 envelope encryption after a successful Master Password sign-in. Legacy trusted unlock is disabled until that migration succeeds.
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
- Local Security Check is local-only: it scans active password entries for weak, exactly reused, and passwords at least 365 days old without returning a secret. Its dialog names affected items, supports protected **Edit selected item**, then rescans after the edit. It performs no network request and does not expose password values in its result.
- State changes preserve up to five paired `.config` + `.storage` snapshots. Restore always restores the pair and locks the vault so the restored credentials must unlock it again.
- Internal snapshots remain on the same disk. They can undo local changes but are not an external backup and do not protect against disk loss.

## Security model and limits

PasswordTool protects data at rest and provides a local second factor for sensitive actions. It is not a substitute for securing Windows itself.

| Protected by the application | Not protected by the application |
| --- | --- |
| Vault entries and TOTP secret are encrypted with AES-256-GCM. | Malware, a compromised running Windows session, screen capture, or memory inspection while the vault is open. |
| Every new vault uses an independent random DEK and KDF salt; cryptographic key buffers are cleared when sessions end where the runtime permits. | Loss of the Master Password, authenticator secret, and usable backups: there is no recovery, reset, backdoor, or cloud copy. |
| The trusted token has separate DPAPI-protected Authenticator-secret and DEK blobs, is bound to the current config, and releases the DEK only after app-level TOTP verification. | Malware or another process already acting as the same Windows user; TOTP is an application gate, not a cryptographic second factor against a compromised Windows account. |
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
| `.config` | Versioned Master key slot (Argon2id metadata plus wrapped DEK), encrypted Authenticator secret, login/security settings, and backup-health timestamps. |
| `.storage` | AES-256-GCM encrypted vault payload. |
| `.trusted-unlock` | Optional, one-day token with separate DPAPI-protected Authenticator-secret and DEK blobs for the current Windows user. |
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

## Windows releases

PasswordTool supports Windows x64. The release workflow publishes a deterministic .NET 10, self-contained, single-file WinForms payload, so end-user machines do not need a separately installed .NET runtime. It has no runtime network, update, telemetry, account, or cloud dependency.

Create an unsigned developer/test release with a new numeric `major.minor.patch` version:

```powershell
pwsh .\scripts\Publish-WindowsRelease.ps1 -Version 1.0.0
```

The script writes a non-overwriting versioned directory under `artifacts\releases\1.0.0`. It validates the published payload, excludes vault and source inputs, produces a portable ZIP, and writes SHA-256 checksums, a public static release manifest, and an explicit `release-status.txt`. Do not distribute a release whose status says `UNSIGNED` as a production-signed release.

Qualify the newly generated, finalized directory without modifying it:

```powershell
pwsh .\scripts\Test-ReleasePipeline.ps1
pwsh .\scripts\Test-ReleaseQualification.ps1
pwsh .\scripts\Invoke-ReleaseQualification.ps1 -ReleaseDirectory .\artifacts\releases\1.0.0
```

Qualification independently re-hashes the payload and distribution artifacts, compares ZIP entries with the published files, enforces the public manifest schema, rejects API/source/test/vault/secret content, and verifies the offline release and installer source contracts. An unsigned developer release, unavailable signing or installer tools, and pending manual installer/application smoke checks are reported as limitations, not as production release readiness.

An Inno Setup template is included for a per-user x64 installer. When `ISCC.exe` is already installed, the release script compiles it and adds the installer to the checksums. When it is unavailable, the script intentionally produces only the portable ZIP and leaves the documented installer handoff; it never downloads a compiler or packaging dependency.

For signing configuration, checksum/signature verification, qualification outcomes, installer data-retention scenarios, the controlled-machine smoke checklist, manual updates, and the operator checklist, read [docs/release-operations.md](docs/release-operations.md).

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
