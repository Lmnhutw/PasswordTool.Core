# PasswordTool.WinForms

The local Windows interface for the encrypted vault and password-hash utility.

## UI responsibilities

- First-launch Master Password and Google Authenticator QR-code setup
- Master Password / trusted-token TOTP unlock choices and sign-in preference UI
- Vault list, protected secret reveal/edit dialogs, recovery-code review, and backup file dialogs
- Local search, favorites/folders/tags, password generation, website TOTP copy, and CSV import review
- Password-hash selection, generation, verification, and inspection
- Clipboard shortcut blocking and normal keyboard navigation

## Boundary with Core

This project owns forms and interaction only. Master Password processing, key derivation, TOTP secret handling, encryption, storage, item validation, backup parsing, and import conflict logic must remain in `PasswordTool.Core`.

The UI must never store, log, or display an encryption key or raw TOTP secret. When it requests a password, recovery codes, backup export/import, or protected edit, it must obtain the current TOTP code through `VaultService` rather than duplicating verification logic.

General copy/cut shortcuts remain disabled as a guard against accidental exposure. Explicit username, password, and website-TOTP copy actions clear the clipboard after 30 seconds only when it is unchanged. Clipboard clearing is not a security boundary: Windows, clipboard history, malware, or another process may read copied content first.
