# PasswordTool.Core

Reusable business and security layer for PasswordTool.

The vault implementation in this project owns:

- Master Password validation and PBKDF2 key derivation
- AES-GCM encryption/decryption
- Encrypted local vault storage under `%LocalAppData%\PasswordTool`
- TOTP secret generation and verification with `Otp.NET`
- Vault item models and CRUD services

The local storage files use non-obvious names, `.config` and `.storage`, and are marked Hidden/System on Windows where possible. Those attributes are only obfuscation; security depends on the Master Password-derived encryption key.
