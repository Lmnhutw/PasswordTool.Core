Purpose: Add Windows lifecycle locking and configurable security timers to the existing offline PasswordTool desktop vault.

Assumptions: Keep .NET 10, WinForms, the local/offline architecture, English UI, current encryption and trusted-token formats, and compatibility with existing config/vault/backup files. Phase 1 Backup & Recovery Center is complete. Use 10 minutes as the legacy/default inactivity timeout and 5 minutes as the legacy/default sensitive-action timeout.

Final Prompt:

You are implementing **Phase 2 of 5: Windows lifecycle locking and configurable security timers** for the existing PasswordTool repository.

## Objective

Ensure decrypted vault state is cleared when the Windows user session is no longer safely interactive, and let users configure bounded inactivity and sensitive-action timeouts without weakening the existing authentication boundaries.

Implement **only Phase 2**. Do not begin password-lifecycle metadata, installer/release, or release-qualification work from Phases 3–5.

## Existing context

The solution targets .NET 10 and contains:

- `PasswordTool.Core`: configuration, vault state, cryptography, validation, persistence, and security workflows.
- `PasswordTool.WinForms`: desktop interaction, Windows lifecycle integration, timers, and settings UI.
- `PasswordTool.Api`: a local-development password-hashing API that must not expose vault operations.
- `PasswordTool.Core.Tests`: Core behavior and security regression tests.

The application already has explicit locking, system-idle detection, an in-memory sensitive-action authorization window, a single-instance guard, paired config/vault persistence, Master Password authorization, and Phase 1 backup/recovery behavior.

Inspect the current source before changing it. Reuse the existing normal lock/close path so session cleanup remains centralized.

## Scope

### 1. Version-tolerant security timer settings

Add persisted configuration for:

- inactivity lock timeout, default 10 minutes;
- sensitive-action authorization timeout, default 5 minutes.

Requirements:

- Older config files without these fields must load with the defaults.
- Inactivity timeout must be limited to 1–120 minutes.
- Sensitive-action timeout must be limited to 1–30 minutes.
- Do not provide a disabled or “never lock” value.
- Validate persisted and requested values in `PasswordTool.Core` before use.
- Preserve both settings through Master Password rotation, KDF upgrades, Authenticator rotation, backup-health updates, snapshots, and other config rewrites.

Use a focused model such as `VaultSecuritySettings`; do not introduce a general settings framework.

### 2. Core-owned authorization and session behavior

Add or extend a Core workflow that atomically saves sign-in mode and security timers.

Requirements:

- Require the current Master Password before saving either timer.
- Invalid credentials or out-of-range values must leave the config unchanged.
- A successful settings update must clear any currently active sensitive-action authorization window.
- A successful application-Authenticator verification must use the configured sensitive-action timeout.
- Sensitive authorization remains in memory only and is cleared whenever the vault session closes.
- Do not change the one-day trusted-unlock-token lifetime in this phase.

WinForms must not write `AppConfig` directly or reproduce validation rules.

### 3. Windows lifecycle locking

While the unlocked vault window is open, lock through the existing normal lock path when Windows reports:

- workstation/session lock;
- console disconnect;
- Remote Desktop disconnect;
- power suspend;
- power resume, as a fail-safe when suspend notification was unavailable or delayed.

Requirements:

- Subscribe to Windows lifecycle events only while the vault window is active and always unsubscribe when it closes.
- Marshal callbacks safely to the WinForms UI thread.
- Make repeated or racing lock requests idempotent.
- Closing through this path must clear the decrypted vault, key material, application Authenticator secret, sensitive-action authorization, and any clipboard value still owned by PasswordTool.
- Do not lock merely because the user switches to another application.

### 4. Configurable inactivity locking

- Replace the hard-coded 10-minute check with the validated Core setting.
- Refresh the active inactivity duration after the Settings dialog closes.
- If the configured idle threshold has already elapsed, the next timer check must lock normally.
- Preserve the current Windows `GetLastInputInfo` system-idle behavior.

### 5. WinForms settings UI

Extend the existing Settings form with numeric minute controls for:

- inactivity lock: 1–120;
- sensitive actions: 1–30.

The UI must:

- show persisted values;
- state that saving requires the Master Password;
- state that Windows lock/suspend lifecycle locking always applies;
- save sign-in mode and timers as one Core-owned operation;
- retain ordinary keyboard navigation and standard Windows controls.

Do not create a replacement settings architecture or duplicate the existing form.

## Constraints

- Keep WinForms and .NET 10.
- Keep the application offline.
- Do not add cloud sync, accounts, telemetry, database storage, browser integration, or background services.
- Do not change vault encryption, backup encryption, KDF parameters, trusted-token format/lifetime, or TOTP algorithms.
- Do not expose vault settings or operations through `PasswordTool.Api`.
- Do not add dependency-injection, mediator, event-bus, repository, or generic configuration abstractions.
- Preserve Phase 1 recovery and backup-health behavior.
- Preserve existing working-tree changes and do not create a commit unless explicitly requested.

## Required tests

Add focused tests proving:

1. New and legacy configs use 10-minute inactivity and 5-minute sensitive-action defaults.
2. Valid settings require the correct Master Password and persist across service instances.
3. Invalid credentials do not mutate settings.
4. Values below or above supported ranges are rejected without mutation.
5. Sensitive-action authorization expires according to the configured duration.
6. Saving settings immediately clears an already-active sensitive-action authorization window.
7. Master Password rotation and KDF upgrade preserve the configured timers and existing Phase 1 config metadata.
8. Existing sign-in-mode behavior remains compatible.

Windows session/power event wiring may be verified by source/build plus documented manual scenarios when automated OS-event testing is not practical.

## Documentation

Update:

- root `README.md`;
- `PRODUCT.md`;
- `docs/architecture.md`;
- relevant Core and WinForms README files.

Document the defaults, supported ranges, Master Password requirement, sensitive-session revocation, Windows lifecycle triggers, legacy-config compatibility, and the fact that “never lock” is not supported.

## Verification

Run in sequence:

```powershell
dotnet restore PasswordTool.slnx
dotnet test PasswordTool.slnx --no-restore
dotnet build PasswordTool.slnx --no-restore
dotnet format PasswordTool.slnx --verify-no-changes --no-restore
dotnet list src\PasswordTool.Api\PasswordTool.Api.csproj package --vulnerable --include-transitive
git diff --check
git status --short
```

If stale MSBuild workers cause the known coverage temp-file access denial, shut down the build servers or rerun test/build with `--disable-build-servers`, and report that variation exactly.

## Completion report

Return:

- concise summary of implemented behavior;
- important files changed;
- security and compatibility guarantees;
- exact restore/test/build/format/package-scan results;
- manual Windows lifecycle scenarios that remain unverified;
- confirmation that only Phase 2 was implemented.
