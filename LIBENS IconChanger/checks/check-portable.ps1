param([string]$AppDirectory = '')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$checkRoot = Join-Path $projectRoot ('artifacts\portable-check-' + [Guid]::NewGuid().ToString('N'))
$copyRoot = Join-Path $checkRoot 'relocated LIBENS IconChanger'
$reports = Join-Path $checkRoot 'reports'
New-Item -ItemType Directory -Path $copyRoot,$reports -Force | Out-Null
$sourceRoot = if ($AppDirectory) { [IO.Path]::GetFullPath($AppDirectory, (Get-Location).ProviderPath) } else { Join-Path $projectRoot 'essential' }
Get-ChildItem -LiteralPath $sourceRoot | Copy-Item -Destination $copyRoot -Recurse -Force
$runtimeConfig = Get-Content -LiteralPath (Join-Path $copyRoot 'LIBENS IconChanger.runtimeconfig.json') -Raw | ConvertFrom-Json
if ($runtimeConfig.runtimeOptions.framework -or $runtimeConfig.runtimeOptions.frameworks) { throw 'Shared framework dependency remains.' }
if (-not $runtimeConfig.runtimeOptions.includedFrameworks) { throw 'Self-contained runtime metadata missing.' }
$trace = Join-Path $reports 'host-trace.txt'
$start = [Diagnostics.ProcessStartInfo]::new()
$start.FileName = Join-Path $copyRoot 'LIBENS IconChanger.exe'
$start.WorkingDirectory = $reports
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
$start.ArgumentList.Add('--ui-check-native')
$start.ArgumentList.Add($reports)
$start.Environment['PATH'] = Join-Path $env:SystemRoot 'System32'
$start.Environment['DOTNET_ROOT'] = Join-Path $checkRoot 'no-installed-dotnet'
$start.Environment['DOTNET_ROOT_X64'] = $start.Environment['DOTNET_ROOT']
$start.Environment['DOTNET_MULTILEVEL_LOOKUP'] = '0'
$start.Environment['COREHOST_TRACE'] = '1'
$start.Environment['COREHOST_TRACEFILE'] = $trace
$start.Environment['DOTNET_HOST_TRACE'] = '1'
$start.Environment['DOTNET_HOST_TRACEFILE'] = $trace
$process = [Diagnostics.Process]::Start($start)
if (-not $process.WaitForExit(30000)) { $process.Kill(); throw 'Portable UI check timed out.' }
if ($process.ExitCode -ne 0) { throw "Portable UI exit code: $($process.ExitCode)" }
if (-not (Test-Path -LiteralPath (Join-Path $reports 'ui-native-dpi.png'))) { throw 'UI render not produced.' }
$hostTrace = Get-Content -LiteralPath $trace -Raw
$expectedCore = Join-Path $copyRoot 'coreclr.dll'
if (-not $hostTrace.Contains($expectedCore)) { throw 'Local CoreCLR path was not confirmed by host trace.' }
$files = @(Get-ChildItem -LiteralPath $copyRoot -Recurse -File)
$result = [pscustomobject]@{
    Passed = $true
    RelocatedFolder = $copyRoot
    WorkingDirectory = $reports
    ExitCode = $process.ExitCode
    LocalCoreClr = $expectedCore
    IncludedFrameworks = $runtimeConfig.runtimeOptions.includedFrameworks
    FileCount = $files.Count
    SizeMB = [Math]::Round(($files | Measure-Object Length -Sum).Sum / 1MB, 1)
    Scope = 'Relocated copy on this Windows x64 PC; shared .NET resolution disabled; read-only UI smoke check.'
}
$result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $reports 'result.json') -Encoding utf8
$result | ConvertTo-Json -Depth 5
