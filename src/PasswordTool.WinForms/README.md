# PasswordTool.WinForms

Local WinForms UI for the encrypted PasswordTool vault.

This project contains forms only. Master Password handling, key derivation, encryption, vault storage, and TOTP verification live in `PasswordTool.Core`.

Login flow:

1. `UnlockVaultForm` lets the user choose `Master Password` or `Google Authenticator`.
2. `Master Password` login calls `PasswordTool.Core` to derive the encryption key, open the vault, and create a 1-day trusted Google Authenticator token.
3. `Google Authenticator` login is enabled only while that token is valid; Core verifies the current 6-digit code before `VaultForm` opens.

The WinForms project must not store the Master Password, raw TOTP secret, or encryption key.

Required forms:

- `MainForm.cs`
- `PasswordHashToolForm.cs`
- `CreateMasterPasswordForm.cs`
- `UnlockVaultForm.cs`
- `SetupAuthenticatorForm.cs`
- `VerifyTotpForm.cs`
- `VaultForm.cs`
- `VaultItemEditorForm.cs`
- `RecoveryCodesReviewForm.cs`
- `BackupPassphraseForm.cs`
- `VaultImportReviewForm.cs`

`VaultItemEditorForm` supports password and recovery-code items. Recovery-code input is read directly from the Windows clipboard only after the user clicks `Paste from Clipboard & Review`; parsed codes are shown for confirmation before they can be saved.

`VaultForm` exposes encrypted JSON export/import. WinForms owns file pickers, passphrase prompts, TOTP prompts, and review dialogs, while parsing, encryption, validation, conflict classification, and atomic vault mutation remain in `PasswordTool.Core`.
