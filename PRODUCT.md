# Product brief

## Product

PasswordTool is a local Windows password vault with an accompanying password-hash utility.

## Users and purpose

For individuals who want to keep personal credentials and recovery codes on their own Windows device, without cloud sync, a hosted account, or an external database. The core workflow is to unlock, find, review, and safely update a credential with minimal friction.

## Experience principles

- **Calm and minimal:** prioritize the vault task over decorative dashboards or security theatrics.
- **Explicit security state:** explain when a Master Password, TOTP code, trusted-device token, or backup passphrase is required and why.
- **Deliberate sensitive actions:** require confirmation for sign-in preference changes, revealing secrets, and backup export/import.
- **Familiar desktop behavior:** preserve keyboard navigation, visible focus, and standard Windows control patterns.
- **No colour-only meaning:** pair status colour with concise visible text or an icon.

## Security posture in the interface

The interface should help users understand that the Master Password cannot be recovered; that Google Authenticator depends on a one-day local trusted token; and that backup passphrases are separate from the Master Password. Avoid claims that clipboard blocking, hidden files, or a TOTP code alone make data safe from a compromised Windows session.

## Intentional constraints

- No cloud sync, online account, reset flow, or backdoor.
- No in-app copy/cut actions for vault secrets.
- Hidden URL and Notes fields remain encrypted and should be shown as `Hidden` in lists, not silently omitted.