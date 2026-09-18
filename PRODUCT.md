# Product brief

## Product

PasswordTool is a local Windows vault for passwords, website TOTP codes, and recovery codes, with an accompanying password-hash utility.

## Users and purpose

For individuals who want to keep personal credentials, website TOTP secrets, and recovery codes on their own Windows device, without cloud sync, a hosted account, or an external database. The core workflow is to unlock, find, copy, review, and safely update a credential with minimal friction.

## Experience principles

- **Calm and minimal:** prioritize the vault task over decorative dashboards or security theatrics.
- **Explicit security state:** explain when a Master Password, TOTP code, trusted-device token, or backup passphrase is required and why.
- **Lifecycle-aware locking:** clear the decrypted vault session when Windows locks, disconnects, suspends, or resumes; make inactivity and sensitive-action durations explicit and bounded.
- **Deliberate sensitive actions:** require confirmation for sign-in preference changes, revealing secrets, and backup export/import.
- **Recoverable without an account:** make encrypted external backup creation, verification, and first-launch recovery understandable without implying that local snapshots protect against disk loss.
- **Practical daily use:** make search, password generation, short-lived copy actions, and common CSV migration easy without adding an online service.
- **Actionable local review:** Security Check findings must remain secret-free, distinguish general item edits from password-age changes, and route remediation through the existing protected editor.
- **Familiar desktop behavior:** preserve keyboard navigation, visible focus, and standard Windows control patterns.
- **No colour-only meaning:** pair status colour with concise visible text or an icon.

## Security posture in the interface

The interface should help users understand that the Master Password cannot be recovered; that Google Authenticator depends on a one-day local trusted token; and that backup passphrases are separate from the Master Password. New-machine recovery authenticates an encrypted backup, then creates a new Master Password and a new PasswordTool Authenticator; the backup never carries the old application Authenticator configuration or trusted token. Internal snapshots stay on the same disk and must not be presented as disaster-recovery protection. Avoid claims that clipboard blocking, hidden files, or a TOTP code alone make data safe from a compromised Windows session.

Security timeout changes require the current Master Password. The interface may offer 1–120 minutes for inactivity locking and 1–30 minutes for sensitive-action authorization, defaulting to 10 and 5 minutes respectively. It must not offer a “never lock” option.

## Intentional constraints

- No cloud sync, online account, reset flow, or backdoor.
- No server, telemetry, browser extension, mobile client, passkeys, sharing, or attachments.
- Copy actions are explicit and clear unchanged clipboard values after 30 seconds; this is not protection from a compromised Windows session.
- Hidden URL and Notes fields remain encrypted and should be shown as `Hidden` in lists, not silently omitted.
