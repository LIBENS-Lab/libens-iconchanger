# LIBENS IconChanger

A free icon-management app for Windows 10/11 x64. Download `LIBENS-IconChanger-1.4.1-win-x64.zip` from GitHub Releases, right-click it and choose **Extract All**, then open `essential` and double-click `LIBENS IconChanger.exe`. Do not launch from inside the ZIP. No installer, separate .NET installation or source build is required. Copy the whole essential folder when moving it: the EXE needs the adjacent runtime files and assets.

## Language

Use `KO / EN` at the top right to select Korean or English. The default follows Windows: Korean for Korean systems, English otherwise. In PowerShell, run `& '.\LIBENS IconChanger.exe' --lang en` or use `--lang ko` to choose explicitly. Switching languages keeps entered paths and option values. Administrator launch carries the selected language into the new window.

## Change an icon

1. Choose File types and enter extensions, or choose Windows icons and select a target.
2. Select an image, ICO, EXE or DLL. Multiple extensions may be separated with commas, semicolons or spaces.
3. If using EXE/DLL as the icon source, choose its icon index. This option concerns the source file, not the target extension.
4. Adjust Corner radius if needed. It applies only to rectangular images with four opaque corners; existing transparency is preserved.
5. For Shortcut arrow overlay, adjust Overlay size. At 50%, the image's linear size is halved inside the original canvas, aligned bottom-left with transparent padding.
6. Check the preview, click Apply icon and confirm the displayed scope.
7. For system icons, accept the cache-refresh prompt when ready. Finish file copy, move and delete operations first: Explorer windows and the taskbar briefly close and reopen.

Supported formats include PNG, JPG/JPEG, GIF, BMP, TIFF, WEBP, SVG, ICO, EXE and DLL. WEBP and SVG decoding depends on the image and thumbnail support available in Windows.

## EXE icons and system scope

1. Entering `.exe` shows Only EXEs without embedded icons. When checked, embedded icons remain intact and only the shared fallback changes. Administrator permission is required.
2. Unchecking it assigns one EXE icon for the current user. It does not edit EXE contents, launch commands or digital signatures. An earlier all-users fallback remains until restored.
3. Default folders, drive and printer icons, EXE fallback icons and shortcut overlays apply to all users. Choose Open as admin, approve Windows' prompt and apply in the new window. The original window closes only after the new process starts.
4. If another administrator account is used, current-user changes apply to that account.
5. This PC, Recycle Bin, Network and User files use current-user desktop icon settings.
6. The shortcut overlay is shared by all shortcuts. Individual shortcut targets and icons are not edited.
7. Folder thumbnails, custom folders, special folders and manufacturer-specific icons can retain their own appearance.

## Restore and stored files

1. Select an entry in History and restore, then click Restore selected. Restoring all-users settings requires administrator permissions.
2. Related states, such as Recycle Bin empty/full, can be restored as one group.
3. Backups and generated ICO files use `%LOCALAPPDATA%\FileIconManager` for the current user and `%PROGRAMDATA%\FileIconManager` for all users. The old directory name is retained for compatibility.
4. New icon changes store an independent multi-resolution ICO. After a successful apply, deleting the input image does not affect the stored icon. Do not delete the generated ICO folder or backups.
5. The first original setting for each target is retained. Reapplying the same target does not create successive original backups. Different targets and ICO content can add files; automatic ICO cleanup is not implemented.
6. Copying the app does not transfer another PC's settings or backups.
7. Corrupt backups block writes for the affected scope while preserving the original backup file.

## Refresh limitations

Explorer may cache images until a refresh, restart or sign-out. Cache refresh deletes only automatically generated icon and thumbnail caches after confirmation; it does not delete documents or photos. Windows versions, views, security policies and icon handlers can affect the result. This tool does not replace every icon in every Windows screen.
