# LIBENS IconChanger

[한국어](README.ko.md) · [User guide](<LIBENS IconChanger/essential/USER-GUIDE.en.md>) · [Changelog](CHANGELOG.md)

<img src="LIBENS%20IconChanger/essential/source/assets/app.png" width="80" alt="LIBENS IconChanger icon">

A free, MIT-licensed Windows desktop utility for changing file-type and Windows system icons, with original-setting backup and restore. Includes Korean and English interfaces. Current version: **1.4.1**.

1. Change multiple file extensions at once.
2. Customize default folders, drives, Recycle Bin states and the shared shortcut-arrow overlay.
3. Convert images to multi-resolution ICO files, adjust corner rounding and scale shortcut overlays inside a transparent canvas.
4. Restore original settings from backup history.

## Download and run (no build required)

1. [Download the Windows x64 ZIP](https://github.com/LIBENS-Lab/libens-iconchanger/releases/download/v1.4.1/LIBENS-IconChanger-1.4.1-win-x64.zip) or visit [Releases](https://github.com/LIBENS-Lab/libens-iconchanger/releases/latest).
2. Right-click the ZIP and choose **Extract All**.
3. Open `essential` in the extracted folder and double-click **LIBENS IconChanger.exe**.

No installer, separate .NET installation, or source build is required. Keep the whole `essential` folder together. The GitHub `Code > Download ZIP` and `Source code` downloads contain developer source, not the ready-to-run app. The app is not code-signed; Windows may display a security warning. Do not disable Windows security protections.

## Build from source (developers)

For development, use Windows x64, PowerShell 7 and the .NET 10 SDK. Run from the repository root:

```powershell
dotnet build 'LIBENS IconChanger/essential/source/LIBENS IconChanger.csproj' -c Debug
& './LIBENS IconChanger/artifacts/bin/Debug/net10.0-windows/LIBENS IconChanger.exe' --lang en
```

Choose `KO / EN` in the app to switch languages. The initial language follows Windows: Korean on Korean systems, English otherwise. Use `--lang ko` or `--lang en` to override it for a launch.

To create a self-contained Windows x64 folder:

```powershell
& './LIBENS IconChanger/build-essential.ps1'
```

Run `LIBENS IconChanger/essential/LIBENS IconChanger.exe`. Copy the whole `essential` folder, including its runtime and resources, when moving the app. Generated binaries are excluded from Git.

## Documentation

1. [English user guide](<LIBENS IconChanger/essential/USER-GUIDE.en.md>)
2. [한국어 사용방법](<LIBENS IconChanger/essential/사용방법.md>)
3. [개발 및 검증 안내](<LIBENS IconChanger/README.md>)

## Scope and data compatibility

System-wide icons require administrator permission. The app shows the scope and asks before applying changes or restarting Explorer. Folder content thumbnails and custom folder icons may differ from the default folder icon.

Existing data locations remain `%LOCALAPPDATA%\FileIconManager` and `%PROGRAMDATA%\FileIconManager` so earlier backups and stored icons remain usable after the product rename. User settings and backups are not part of this source repository.

## Development and support

1. [Contributing and local checks](CONTRIBUTING.md)
2. [Security reporting](SECURITY.md)
3. [Third-party notices](THIRD-PARTY-NOTICES.md)
4. [GitHub publication guide / 공개 안내](PUBLICATION.md)

The GitHub Actions workflow builds on Windows and runs isolated backup/restore checks and both UI languages. It does not change actual user icon settings or publish a release automatically.

## License

[MIT](LICENSE), copyright 2026 LIBENS. See [third-party notices](THIRD-PARTY-NOTICES.md) for runtime components and externally supplied artwork. Windows system icons are read from the installed operating system rather than included as a redistributable icon pack.
