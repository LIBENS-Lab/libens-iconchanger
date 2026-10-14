$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    .\build-native.ps1 -Source ShellProbe.cpp
    $probe = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\artifacts\native\x64\ShellProbe.exe'))
    $icon = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\essential\source\assets\app.ico'))
    $logs = [Collections.Generic.List[object]]::new()
    function Invoke-Probe([string]$Mode, [string]$Target) {
        $lines = & $probe $Mode $icon $Target
        if ($LASTEXITCODE -ne 0) { throw "Probe failed: $LASTEXITCODE" }
        $raw = $lines -join "`n"
        $logs.Add(@{mode=$Mode; target=$Target; output=$raw})
        return $raw
    }
    function Icon-Hash([string]$Raw, [string]$Pattern) {
        if ($Raw -notmatch $Pattern) { throw "Missing result: $Pattern" }
        return $Matches[1]
    }
    $plainBase = Invoke-Probe baseline $probe
    $plainChanged = Invoke-Probe machine $probe
    $customBase = Invoke-Probe baseline "$env:WINDIR\System32\notepad.exe"
    $customChanged = Invoke-Probe machine "$env:WINDIR\System32\notepad.exe"
    $customAll = Invoke-Probe all-exe "$env:WINDIR\System32\notepad.exe"
    $newHash = Icon-Hash $plainChanged 'Stock 2:.*hash=([0-9a-f]+)'
    if ((Icon-Hash $plainChanged 'File icon hash=([0-9a-f]+)') -ne $newHash) { throw 'Iconless EXE fallback failed' }
    if ((Icon-Hash $plainBase 'File icon hash=([0-9a-f]+)') -eq $newHash) { throw 'Fallback did not change' }
    if ((Icon-Hash $customBase 'File icon hash=([0-9a-f]+)') -ne (Icon-Hash $customChanged 'File icon hash=([0-9a-f]+)')) { throw 'Embedded EXE icon was changed' }
    if ((Icon-Hash $customAll 'File icon hash=([0-9a-f]+)') -ne $newHash) { throw 'EXE all-icons mode failed' }
    foreach ($stock in @(0,1,2,3,4,7,8,9,10,11,16,29)) {
        if ((Icon-Hash $plainChanged "Stock ${stock}:.*hash=([0-9a-f]+)") -ne $newHash) { throw "Stock icon $stock not replaced" }
    }
    foreach ($clsid in @('{20D04FE0-3AEA-1069-A2D8-08002B30309D}','{645FF040-5081-101B-9F08-00AA002F954E}',
        '{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}','{59031a47-3f72-44a7-89c5-5595fe6b30ee}')) {
        $original = Invoke-Probe baseline "::$clsid"
        $changed = Invoke-Probe user "::$clsid"
        $desktopHash = Icon-Hash $changed 'Reference hash=([0-9a-f]+)'
        if ($desktopHash -eq '0' -or (Icon-Hash $changed 'Namespace: 0 hash=([0-9a-f]+)') -ne $desktopHash) { throw "Desktop icon $clsid not replaced" }
        if ((Icon-Hash $original 'Namespace: 0 hash=([0-9a-f]+)') -eq $desktopHash) { throw "Desktop icon $clsid unchanged" }
    }
    $result = @{ os = [Environment]::OSVersion.VersionString; status='PASS'; logs=$logs }
    $result | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $PSScriptRoot '..\artifacts\native\shell-results.json') -Encoding utf8
    Write-Output 'PASS: iconless/custom EXE, all-EXE, twelve stock IDs including shortcut overlay, four desktop namespaces (process-local registry)'
}
finally { Pop-Location }
