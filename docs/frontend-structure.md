# Frontend Structure Contract

> Source of truth for presentation structure and naming in the WinForms client.

## Scope

- Frontend root: `src/PasswordTool.WinForms`
- Framework: .NET Windows Forms
- Styling system: code-based WinForms controls with the shared `UiTheme`
- Applies to: all forms, dialogs, menus, status surfaces, and grids in the Windows client

## Naming Convention

- Name controls and helpers by responsibility, such as `sensitiveSessionPanel` or `folderFilterComboBox`.
- Do not name controls after coordinates, colors, or incidental visual appearance.
- Use `*Form` for top-level windows and `Create*`, `Configure*`, `Style*`, or `Update*` for focused presentation helpers.
- This client has no CSS or DOM, so CSS/BEM naming does not apply.

## Ownership

| Area | Responsibility | Location |
| --- | --- | --- |
| Shared visual language | Color, typography, button, menu, input, and grid styling | `src/PasswordTool.WinForms/UiTheme.cs` |
| Vault workspace | Header, commands, filters, security status, credential grid, and status bar | `src/PasswordTool.WinForms/VaultForm.cs` |
| Authentication | Available login methods and credential input state | `src/PasswordTool.WinForms/UnlockVaultForm.cs` |
| Feature dialogs | Feature-specific layout and interaction only | matching `*Form.cs` file |

## Layout Rules

- Set DPI scaling before building controls; a theme helper must not change a form's scaling mode after layout exists.
- Prefer `TableLayoutPanel`, docking, anchoring, and adequate logical row heights over fixed coordinates.
- A disabled feature must disable every related input and remain understandable through text, not color alone.
- Keep the credential grid as the main Vault surface; secondary commands belong in menus.
- Forms own layout. `UiTheme` owns reusable visual styling and must not contain business or security logic.
- Use the Windows-native title bar, focus behavior, modal behavior, and system dialogs.

## Component Boundaries

- Extract a helper only for repeated styling or a meaningful layout responsibility.
- Do not build a new UI framework around WinForms or duplicate service behavior in presentation code.
- Security authorization, vault storage, encryption, clipboard lifecycle, and timeout policy remain outside the theme and layout layer.

## Cross-Skill Rule

All future UI work must follow this contract and must not introduce a competing styling or naming system.
