# PasswordTool.Core

Reusable business and security layer for PasswordTool.

The vault implementation in this project owns:

- Master Password validation and PBKDF2 key derivation
- AES-GCM encryption/decryption
- Encrypted local vault storage under `%LocalAppData%\PasswordTool`
- Google Authenticator-compatible 6-digit TOTP secret generation and verification with `Otp.NET`
- 1-day trusted unlock tokens protected with Windows DPAPI for Google Authenticator login
- Vault item models and CRUD services

For newly created vaults, `.config` stores non-secret KDF metadata plus the encrypted TOTP secret. The TOTP secret is encrypted with the Master Password-derived key and is only available after the Master Password succeeds. After a successful Master Password login, Core can write `.trusted-unlock`, a 1-day token that stores the vault encryption key protected by Windows DPAPI for the current Windows user. Google Authenticator login requires both a valid TOTP code and that unexpired local token.

The local storage files use non-obvious names, `.config`, `.storage`, and `.trusted-unlock`, and are marked Hidden/System on Windows where possible. Those attributes are only obfuscation; security depends on encryption, DPAPI protection, and the Master Password-derived encryption key.
