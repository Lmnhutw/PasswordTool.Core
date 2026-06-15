# PasswordTool.WinForms

Local WinForms UI for the encrypted PasswordTool vault.

This project contains forms only. Master Password handling, key derivation, encryption, vault storage, and TOTP verification live in `PasswordTool.Core`.

Login flow:

1. `UnlockVaultForm` collects the Master Password and Google Authenticator code.
2. `PasswordTool.Core` derives the encryption key and decrypts the vault data.
3. If the vault has a paired Google Authenticator-compatible TOTP secret, `PasswordTool.Core` verifies the current 6-digit code before `VaultForm` opens.

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
