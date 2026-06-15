# PasswordTool

Local encrypted password vault built with C# and .NET WinForms.

## Prerequisites

- Windows 10 or later
- .NET SDK 10 or later
- Git

Check your installed SDK:

```powershell
dotnet --version
```

## Get the Source

Clone the repository and enter the project folder:

```powershell
git clone <repository-url>
cd PasswordTool.Core
```

If you already downloaded the source as a ZIP, extract it and open a terminal in the extracted folder that contains `PasswordTool.slnx`.

## Restore and Build

Required package references:

```powershell
dotnet add src\PasswordTool.Core\PasswordTool.Core.csproj package Otp.NET
dotnet add src\PasswordTool.WinForms\PasswordTool.WinForms.csproj package QRCoder
```

Restore NuGet packages:

```powershell
dotnet restore PasswordTool.slnx
```

Build the WinForms desktop app:

```powershell
dotnet build src\PasswordTool.WinForms\PasswordTool.WinForms.csproj
```

## Run the Desktop App

Run the WinForms application from source:

```powershell
dotnet run --project src\PasswordTool.WinForms\PasswordTool.WinForms.csproj
```

The desktop window opens locally. On first launch, the app creates a Master Password, pairs Google Authenticator with a QR code, and creates an empty encrypted vault. Other apps that support standard 6-digit TOTP codes can scan the same QR code.

The app does not require a database, cloud service, web server, login system, or internet connection after packages are restored.

## Run Tests

```powershell
dotnet test PasswordTool.slnx
```

## Login and Recovery

New vaults use two steps:

1. Enter the Master Password. PasswordTool derives the vault encryption key from this password.
2. Enter the current 6-digit Google Authenticator code. The vault opens only after the Master Password has been verified and the TOTP code is valid.

Google Authenticator does not replace the Master Password and cannot unlock the encrypted vault by itself. The TOTP secret is encrypted with the Master Password-derived key in `.config`; saved vault items are encrypted separately in `.storage`.

Older vaults that do not have a paired TOTP secret continue to unlock with the Master Password only. Pairing Google Authenticator is required for newly created vaults.

There is no password recovery, authenticator recovery, backdoor, cloud sync, or reset path. Losing the Master Password or the paired authenticator secret can permanently prevent access to the vault.

## Publish a Single-File EXE

Create a self-contained Windows x64 executable:

```powershell
dotnet publish src\PasswordTool.WinForms\PasswordTool.WinForms.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The published executable is created here:

```text
src\PasswordTool.WinForms\bin\Release\net10.0-windows\win-x64\publish\PasswordTool.WinForms.exe
```

## Solution Structure

```text
PasswordTool.slnx
src/
  PasswordTool.Core/
    Models/
      VaultItem.cs
      VaultData.cs
      AppConfig.cs
    Services/
      EncryptionService.cs
      MasterPasswordService.cs
      TotpService.cs
      VaultStorageService.cs
      VaultService.cs
  PasswordTool.WinForms/
    MainForm.cs
    CreateMasterPasswordForm.cs
    UnlockVaultForm.cs
    SetupAuthenticatorForm.cs
    VerifyTotpForm.cs
    VaultForm.cs
    VaultItemEditorForm.cs
  PasswordTool.Api/
tests/
  PasswordTool.Core.Tests/
docs/
  architecture.md
```

## Projects

- `PasswordTool.Core`: reusable vault business logic, models, encryption, encrypted storage, Google Authenticator-compatible TOTP verification, and existing password-hashing helpers.
- `PasswordTool.WinForms`: local desktop UI. This project calls `PasswordTool.Core` and must not contain encryption or key-derivation logic.
- `PasswordTool.Api`: optional ASP.NET Core Minimal API boundary for future service access. This project calls `PasswordTool.Core` and must not duplicate hashing logic.
- `PasswordTool.Core.Tests`: tests for algorithm behavior, parsing, verification, and vault security rules.

## Local Storage

Vault files are stored under:

```text
%LocalAppData%\PasswordTool
```

The app uses `.config` for non-secret KDF metadata plus the encrypted TOTP secret, and `.storage` for the encrypted vault JSON payload. Files are hidden/system on Windows where possible, but the app remains secure if an attacker finds them because saved passwords and the TOTP secret are encrypted.

See [docs/architecture.md](docs/architecture.md) for the design details.
