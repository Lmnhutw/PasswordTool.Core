# Windows release operations

## Release boundary

This is the only supported release-publish path for `PasswordTool.WinForms`:

```powershell
pwsh .\scripts\Publish-WindowsRelease.ps1 -Version 1.0.0
```

It targets `win-x64` and uses the repository's .NET 10 release properties: self-contained, single-file, non-trimmed, deterministic publish with embedded debug information disabled. Output is staged under `artifacts\releases\.staging` and finalized only as `artifacts\releases\<version>`. An existing version directory is always rejected rather than overwritten.

The release script publishes only `src\PasswordTool.WinForms\PasswordTool.WinForms.csproj`; it does not publish `PasswordTool.Api`, test projects, source files, or local user data. Before archiving, it rejects source files, logs, certificates, database files, and the vault paths `.config`, `.storage`, `.trusted-unlock`, and `.snapshots`. It also rejects missing or empty files, output outside its configured staging root, and payloads above the configured size limit.

The resulting directory contains:

- `publish\` — the validated self-contained application payload.
- `PasswordTool-<version>-win-x64.zip` — the portable distribution archive.
- `checksums.sha256` — SHA-256 entries for the application payload and every ZIP/installer distribution artifact.
- `release-manifest.json` — public version, artifact name, SHA-256, and manual-update release notes only.
- `release-status.txt` — `UNSIGNED` developer/test status or `SIGNED` only after signing and verification succeed.

No vault, password, password history, TOTP secret, recovery code, key, backup passphrase, trusted-unlock token, snapshot, or local storage location is packaged, signed, checksummed, or serialized by this flow.

## Installer

`installer\PasswordTool.iss` is a standard Inno Setup template. The release script compiles it only if `ISCC.exe` is already available on the controlled release machine. This repository does not download or add an installer compiler. If the compiler is absent, the ZIP remains the supported reproducible developer artifact and the template is the explicit installer handoff.

When compiled, the installer is per-user and x64 scoped. It installs the validated release payload to `%LocalAppData%\Programs\PasswordTool`, creates a `PasswordTool` Start Menu entry, and keeps the same Inno Setup application identifier for in-place upgrades. It does not request administrator privileges.

Vault data remains in `%LocalAppData%\PasswordTool`, outside the install directory. Upgrade and uninstall do not remove `.config`, `.storage`, `.trusted-unlock`, `.snapshots`, encrypted backups, logs, or any other vault data. The uninstaller removes application binaries only; users must consciously delete local vault material themselves if that is their intent.

## Signing on a controlled release machine

Ordinary development does not require signing. With no signing environment variables, `-SigningMode Auto` produces a deliberately labeled unsigned developer/test release. `-SigningMode Required` fails if credentials or the toolchain are incomplete. `-SigningMode Disabled` is an explicit unsigned mode.

Use one of these certificate inputs, never both:

- `PASSWORDTOOL_SIGN_CERT_THUMBPRINT` — 40-hex-character thumbprint for a certificate available to the controlled release machine.
- `PASSWORDTOOL_SIGN_CERT_PATH` and `PASSWORDTOOL_SIGN_CERT_PASSWORD` — absolute path to a `.pfx`/`.p12` and its secure process environment value.

Also set:

- `PASSWORDTOOL_SIGN_TIMESTAMP_URL` — an approved HTTPS RFC 3161 timestamp service.
- `PASSWORDTOOL_SIGN_TOOL_PATH` — optional absolute path to `signtool.exe`; otherwise the script discovers `signtool.exe` from the installed Windows SDK.

Do not commit a certificate, private key, password, timestamp credential, token, a populated environment file, or a command line containing a real secret. Supply them only through the controlled release process's secure environment. Requested certificate paths must exist and be `.pfx`/`.p12`; signing targets are constrained to the staged release directory. The script signs application and installer executables, then runs `signtool verify /pa /tw`. Any unavailable tool, signing failure, verification failure, or timestamp verification failure stops the release rather than claiming it is signed.

Example, with secret values supplied by the release environment rather than pasted into source:

```powershell
$env:PASSWORDTOOL_SIGN_CERT_THUMBPRINT = '<40-hex-thumbprint>'
$env:PASSWORDTOOL_SIGN_TIMESTAMP_URL = 'https://<approved-timestamp-service>'
pwsh .\scripts\Publish-WindowsRelease.ps1 -Version 1.0.0 -SigningMode Required
```

## Verify a received release

Verify the checksum file from the release directory:

```powershell
Get-Content .\artifacts\releases\1.0.0\checksums.sha256 | ForEach-Object {
    $hash, $relativePath = $_ -split ' \*', 2
    $actual = (Get-FileHash (Join-Path .\artifacts\releases\1.0.0 $relativePath) -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $hash) { throw "Checksum mismatch: $relativePath" }
}
```

For a signed executable, verify its Authenticode signature and timestamp:

```powershell
signtool verify /pa /tw .\artifacts\releases\1.0.0\publish\PasswordTool.WinForms.exe
```

Verify the installer too when one was produced. A successful verification is the only basis for describing the release as signed.

## Manual updates

PasswordTool has no auto-updater, polling service, runtime update check, silent download, or runtime network dependency. Users obtain a newer approved signed installer or portable ZIP through the approved release location, verify it, and run it manually. Normal installer upgrades keep `%LocalAppData%\PasswordTool` intact.

## Release operator checklist

1. Run `pwsh .\scripts\Test-ReleasePipeline.ps1`.
2. Run the solution verification sequence from the root README/task requirements.
3. Choose a new numeric `major.minor.patch` version that does not already exist under `artifacts\releases`.
4. Set signing inputs only on the controlled release machine, or intentionally create an unsigned developer/test release.
5. Run `Publish-WindowsRelease.ps1`; do not manually copy files into its staging/output directory.
6. Inspect `release-status.txt`, `checksums.sha256`, and `release-manifest.json`; verify Authenticode signatures when signing was requested.
7. If Inno Setup was available, smoke-check the installer Start Menu entry and confirm uninstall leaves `%LocalAppData%\PasswordTool` untouched.
8. Publish nothing from this repository automatically. Distribution remains a separate, explicitly authorized action.
