# Password Generator

Generate secure random passwords directly from the Command Palette, or quick-rotate a vault item's password in place.

## Generate Password Command

A top-level **Generate Password** command is available alongside the main vault browser. It opens a form where you can configure:

| Option | Default |
|--------|---------|
| Password Length | 20 |
| Uppercase (A-Z) | On |
| Lowercase (a-z) | On |
| Numbers (0-9) | On |
| Special (!@#$%) | On |

Click **Generate & Copy** to generate a password using the Bitwarden CLI (`bw generate`) and automatically copy it to the clipboard using [secure clipboard](Clipboard-Security) (excluded from clipboard history, auto-cleared).

The form defaults come from the [generator settings](Settings#generator-settings) and can be overridden per-generation.

## Quick Rotate Password

Login items have a **Rotate Password** option in the context menu (right-click or actions shortcut, `Ctrl+Shift+R`).

This opens a form pre-populated with your generator settings where you can:

1. Adjust length/complexity if needed
2. Click **Rotate & Copy** to:
   - Generate a new password with the selected options
   - Update the vault item's password via `bw edit`
   - Trigger a background sync so the change propagates
   - Copy the new password to clipboard (secure)

This streamlines the password rotation workflow — no need to open the Bitwarden desktop client or use the CLI directly.

### Protected Items

For items with master password re-prompt enabled, the rotate action requires re-entering your master password first (respects the [grace period](Settings#re-prompt-grace-period)). The rotation uses the default generator settings and automatically copies the result.
