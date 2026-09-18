# PasswordTool.WinForms

The Windows client provides vault unlock, first-launch backup recovery, a Backup & Recovery Center, explicit secret actions, configurable security timers, settings for Master Password and Authenticator rotation, paired snapshot restore, password history, Trash, and Local Security Check. It runs as a single instance and clears the vault session on configured inactivity or Windows lifecycle boundaries.

The local Windows interface for the encrypted vault and password-hash utility.

## Visual system

The client uses a shared light WinForms theme with Segoe UI typography, consistent primary/secondary/destructive actions, accessible focus states, and common table styling. The main vault keeps the credential table as the primary surface, groups selected-item actions in a compact command bar, and moves application utilities into the **Tools** menu.

Vault search can be combined with compact item-view and folder dropdown filters. These controls only change the local presentation of the already-open vault; encryption, persistence, sensitive-session authorization, and clipboard behavior remain owned by the existing services. Sensitive actions continue to request the Authenticator only when the user invokes them.

## UI responsibilities

- First-launch choice between an empty vault and encrypted-backup recovery, followed by new Master Password and Authenticator setup
- Master Password / trusted-token TOTP unlock choices and sign-in preference UI
- Bounded inactivity/sensitive-action timeout controls and Windows session/power lifecycle locking
- Vault list, protected secret reveal/edit dialogs, recovery-code review, and consolidated external backup/verification/import dialogs
- Local Security Check presentation: safe finding rows, empty state, protected selected-item navigation, and rescan after editing
- Local search, favorites/folders/tags, password generation, website TOTP copy, and CSV import review
- Password-hash selection, generation, verification, and inspection
- Clipboard shortcut blocking and normal keyboard navigation

## Boundary with Core

This project owns forms and interaction only. Master Password processing, key derivation, TOTP secret handling, encryption, storage, item validation, backup parsing, and import conflict logic must remain in `PasswordTool.Core`.

WinForms may select a backup file and display `VaultBackupInspection`, but it must not deserialize or decrypt the backup payload. The Backup & Recovery Center visibly distinguishes external backups from same-disk snapshots and warns when an external backup is missing or older than 30 days.

The UI must never store, log, or display an encryption key or raw TOTP secret. When it requests a password, recovery codes, backup export/import, or protected edit, it must obtain the current TOTP code through `VaultService` rather than duplicating verification logic.

Security Check rows show only item metadata, a friendly finding label, recommendation, and the safe password-change date for old-password findings. The dialog receives no VaultService or password value; it returns a selected item ID so VaultForm reuses the protected editor and rescans afterward. UpdatedAt remains general item metadata; password age comes from Core's PasswordChangedAt lifecycle rule.

General copy/cut shortcuts remain disabled as a guard against accidental exposure. Explicit username, password, and website-TOTP copy actions clear the clipboard after 30 seconds only when it is unchanged. Clipboard clearing is not a security boundary: Windows, clipboard history, malware, or another process may read copied content first.

The vault window subscribes to Windows lifecycle events only while open and unsubscribes when closed. Session lock, console/remote disconnect, suspend, and resume use the normal lock path so Core clears the key and decrypted vault before the unlock screen returns.
