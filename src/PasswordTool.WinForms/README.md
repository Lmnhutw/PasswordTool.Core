# PasswordTool.WinForms

Local WinForms UI for the encrypted PasswordTool vault.

This project contains forms only. Master Password handling, key derivation, encryption, vault storage, and TOTP verification live in `PasswordTool.Core`.

Required forms:

- `MainForm.cs`
- `PasswordHashToolForm.cs`
- `CreateMasterPasswordForm.cs`
- `UnlockVaultForm.cs`
- `SetupAuthenticatorForm.cs`
- `VerifyTotpForm.cs`
- `VaultForm.cs`
- `VaultItemEditorForm.cs`
