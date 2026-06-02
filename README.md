# PasswordTool

Local password hashing learning tool built with C# and .NET.

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
