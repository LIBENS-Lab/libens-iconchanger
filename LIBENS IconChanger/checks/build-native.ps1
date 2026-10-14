param([string]$Source = 'ShellProbe.cpp', [string]$Name = 'ShellProbe', [switch]$Dll, [string]$Arch = 'x64')
$ErrorActionPreference = 'Stop'
$vswhere = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
$vs = & $vswhere -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath | Select-Object -First 1
$msvc = Get-ChildItem "$vs\VC\Tools\MSVC" -Directory | Sort-Object Name -Descending | Select-Object -First 1
$sdkRoot = 'C:\Program Files (x86)\Windows Kits\10'
$sdk = Get-ChildItem "$sdkRoot\Include" -Directory | Where-Object { Test-Path "$($_.FullName)\um\Windows.h" } | Sort-Object Name -Descending | Select-Object -First 1
$env:INCLUDE = "$($msvc.FullName)\include;$($sdk.FullName)\ucrt;$($sdk.FullName)\shared;$($sdk.FullName)\um"
$env:LIB = "$($msvc.FullName)\lib\$Arch;$sdkRoot\Lib\$($sdk.Name)\ucrt\$Arch;$sdkRoot\Lib\$($sdk.Name)\um\$Arch"
$out = Join-Path $PSScriptRoot "..\artifacts\native\$Arch"
New-Item -ItemType Directory -Path $out -Force | Out-Null
$ext = if ($Dll) { '.dll' } else { '.exe' }
$argsNative = @('/nologo', '/W4', '/WX', '/std:c++17', '/EHsc', '/MT', '/O2', '/DUNICODE', '/D_UNICODE', $Source, "/Fo$out\$Name.obj", "/Fe$out\$Name$ext")
if ($Dll) { $argsNative += '/LD' }
$argsNative += @('/link', 'shell32.lib', 'ole32.lib', 'advapi32.lib', 'user32.lib', 'gdi32.lib', "/IMPLIB:$out\$Name.lib")
& "$($msvc.FullName)\bin\Hostx64\$Arch\cl.exe" @argsNative
if ($LASTEXITCODE -ne 0) { throw "Native build failed: $LASTEXITCODE" }
