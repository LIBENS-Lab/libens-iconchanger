# Security

## Reporting a vulnerability

Use this repository's **Security → Advisories → Report a vulnerability** option when private vulnerability reporting is enabled. Include the affected version, reproduction steps, required permissions and impact. Do not publish exploit details or real user backups in a public issue.

If the private reporting option is unavailable, open an issue requesting a private contact channel without including exploitation details. No response-time guarantee is currently offered.

## Project scope

The app changes allowed icon registry values after user confirmation. Machine-wide settings require elevation. It does not edit EXE contents or launch commands. Explorer restart and cache rebuilding require separate confirmation.

Backups and icon assets are stored locally. Backup files can contain private file paths: redact diagnostics and screenshots before sharing. The compatibility directories remain `%LOCALAPPDATA%\FileIconManager` and `%PROGRAMDATA%\FileIconManager`.

Only the current release is actively maintained. Windows updates, icon handlers and security policies can change shell behavior; compatibility with every Windows configuration is not guaranteed.
