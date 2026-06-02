# PasswordTool

Local password hashing learning tool built with C# and .NET.

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

The desktop window opens locally. The app does not require a database, web server, login system, or internet connection after packages are restored.

## Run Tests

```powershell
dotnet test PasswordTool.slnx
```

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
  PasswordTool.WinForms/
  PasswordTool.Api/
tests/
  PasswordTool.Core.Tests/
docs/
  architecture.md
```

## Projects

- `PasswordTool.Core`: reusable password hashing contracts, models, options, hashers, inspection, and security helpers.
- `PasswordTool.WinForms`: local desktop UI. This project calls `PasswordTool.Core` and must not contain hashing logic.
- `PasswordTool.Api`: optional ASP.NET Core Minimal API boundary for future service access. This project calls `PasswordTool.Core` and must not duplicate hashing logic.
- `PasswordTool.Core.Tests`: tests for algorithm behavior, parsing, verification, and security rules.

See [docs/architecture.md](docs/architecture.md) for the design details.
