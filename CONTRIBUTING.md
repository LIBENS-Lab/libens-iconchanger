# Contributing

Small fixes, translations and reproducible bug reports are welcome.

## Development

Use Windows x64, PowerShell 7 and the .NET 10 SDK. From the repository root:

```powershell
dotnet build 'LIBENS IconChanger/essential/source/LIBENS IconChanger.csproj' -c Debug
```

The application source is in `LIBENS IconChanger/essential/source`; build output goes to `LIBENS IconChanger/artifacts`.

## Checks

From `LIBENS IconChanger`:

```powershell
dotnet run --project checks/CoreChecks/CoreChecks.csproj -- artifacts/core-checks-new essential/source/assets/app.ico
& './artifacts/bin/Debug/net10.0-windows/LIBENS IconChanger.exe' --lang ko --ui-check artifacts/ui-ko
& './artifacts/bin/Debug/net10.0-windows/LIBENS IconChanger.exe' --lang en --ui-check artifacts/ui-en
```

CoreChecks redirects registry access into a temporary HKCU subtree for that process. Use a fresh output directory when rerunning: existing test backup files can affect results. UI checks use sample data and do not change icon settings.

Additional shell pixel checks require Visual Studio C++ Build Tools and the Windows SDK. Run `checks/check-shell.ps1` when changing system-icon behavior. Run `checks/check-contrast.ps1` when changing colors. Portable builds are checked with `checks/check-portable.ps1`.

## Design and compatibility

1. Keep user input, file paths, registry targets and saved backup identities independent of display language.
2. Add English translations to `essential/source/Translations.en.json` alongside Korean app strings; check language switching and both layouts.
3. Use the centralized `Theme` colors for app-owned controls and their states.
4. Preserve the first original backup. Do not silently migrate stored ICO paths or broaden allowed registry targets.
5. Confirm before system-wide changes, elevation or Explorer restart. Never perform those operations in ordinary startup or preview.
6. Include only app-owned or appropriately licensed assets and retain required notices.

## Pull requests

1. Explain the user-facing problem and resulting behavior.
2. Include relevant check results and sanitized screenshots when useful.
3. Keep unrelated changes out of the same pull request.
4. Report security issues through the process in [SECURITY.md](SECURITY.md).
