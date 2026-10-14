# Third-party components and assets

The project source does not currently declare external NuGet `PackageReference` dependencies. It targets .NET 10 Windows Forms and calls Windows APIs supplied by the operating system.

Self-contained builds include .NET and Windows Desktop runtime components. These have their own licenses and third-party notices. `build-essential.ps1` copies the notices from the actual restored runtime packages to `essential/licenses`. Keep this directory with distributed runtime builds.

1. [.NET runtime licensing](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT)
2. [Windows Forms licensing](https://github.com/dotnet/winforms/blob/main/LICENSE.TXT)
3. [Windows Desktop licensing](https://github.com/dotnet/windowsdesktop/blob/main/LICENSE)

The application icon source is `LIBENS IconChanger/essential/source/assets/app.svg`; PNG and ICO variants are included alongside it. Windows system icons displayed in the app are read at runtime from the user's installed Windows system, rather than bundled as a redistributable icon collection.

User-selected images are not supplied by this project. Their license remains the responsibility of whoever distributes those images or an icon pack. The project's own license does not grant rights to third-party runtime components, Windows icons or user-selected artwork.
