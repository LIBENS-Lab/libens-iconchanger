#Requires -Version 7.0
param(
    [string]$OutputDirectory = '',
    [ValidatePattern('^10\.0\.\d+$')]
    [string]$RuntimeFrameworkVersion = '10.0.13'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$projectRoot = Join-Path $repositoryRoot 'LIBENS IconChanger'
$projectFile = Join-Path $projectRoot 'essential/source/LIBENS IconChanger.csproj'
[xml]$projectXml = Get-Content -LiteralPath $projectFile -Raw
$version = [string]$projectXml.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid release version.' }
$outputRoot = if ($OutputDirectory) {
    [IO.Path]::GetFullPath($OutputDirectory, (Get-Location).ProviderPath)
} else {
    Join-Path $projectRoot "artifacts/releases/$version"
}
$zipPath = Join-Path $outputRoot "LIBENS-IconChanger-$version-win-x64.zip"
$checksumPath = Join-Path $outputRoot 'SHA256SUMS.txt'
foreach ($outputFile in @($zipPath, $checksumPath)) {
    if (Test-Path -LiteralPath $outputFile) { throw "Output already exists: $outputFile" }
}
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$bundleRoot = Join-Path $outputRoot ('bundle-' + [Guid]::NewGuid().ToString('N'))
$essential = Join-Path $bundleRoot 'essential'
& (Join-Path $projectRoot 'build-essential.ps1') -OutputDirectory $essential -RuntimeFrameworkVersion $RuntimeFrameworkVersion

foreach ($required in @(
    'LIBENS IconChanger.exe', 'LIBENS IconChanger.dll',
    'LIBENS IconChanger.runtimeconfig.json', 'coreclr.dll', 'hostfxr.dll',
    'hostpolicy.dll', 'System.Windows.Forms.dll', 'assets/app.ico',
    'LICENSE', 'THIRD-PARTY-NOTICES.md', '사용방법.md', 'USER-GUIDE.en.md',
    'licenses/Microsoft.NETCore.App/LICENSE.TXT',
    'licenses/Microsoft.WindowsDesktop.App/LICENSE',
    'source/LIBENS IconChanger.csproj'
)) {
    if (-not (Test-Path -LiteralPath (Join-Path $essential $required) -PathType Leaf)) {
        throw "Distribution file missing: $required"
    }
}
$runtime = Get-Content -LiteralPath (Join-Path $essential 'LIBENS IconChanger.runtimeconfig.json') -Raw | ConvertFrom-Json
if ($runtime.runtimeOptions.framework -or $runtime.runtimeOptions.frameworks) {
    throw 'The app still requires a separately installed .NET runtime.'
}
$frameworks = @($runtime.runtimeOptions.includedFrameworks)
if ($frameworks.Count -ne 2 -or @($frameworks | Where-Object version -NE $RuntimeFrameworkVersion).Count) {
    throw 'Unexpected bundled runtime version.'
}
foreach ($bundleFile in Get-ChildItem -LiteralPath $essential -Recurse -File) {
    $relative = [IO.Path]::GetRelativePath($essential, $bundleFile.FullName).Replace('\', '/')
    if ($relative -match '(^|/)(\.git|artifacts|obj|bin|archive|generated-icons)(/|$)' -or
        $relative -match '(^|/)(AGENTS\.md|지시사항[^/]*|작업 인계\.md|backups\.json[^/]*|\.env[^/]*)$' -or
        $relative -match '\.(pfx|p12|key|lnk|log|dmp)$') {
        throw "Unexpected private or development output: $relative"
    }
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'release/START-HERE.txt') -Destination $bundleRoot
Compress-Archive -LiteralPath $essential, (Join-Path $bundleRoot 'START-HERE.txt') -DestinationPath $zipPath -CompressionLevel Optimal
$zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
"$zipHash  $([IO.Path]::GetFileName($zipPath))" | Set-Content -LiteralPath $checksumPath -Encoding utf8NoBOM
$result = [pscustomobject]@{
    Version = $version
    Architecture = 'win-x64'
    BundledRuntime = $RuntimeFrameworkVersion
    BundleRoot = $bundleRoot
    EssentialDirectory = $essential
    ZipFile = $zipPath
    ChecksumFile = $checksumPath
    SHA256 = $zipHash
    ZipSizeMB = [Math]::Round((Get-Item -LiteralPath $zipPath).Length / 1MB, 1)
    Scope = 'Packaging checks only; extract and run portable checks before publishing.'
}
$result | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputRoot 'package-result.json') -Encoding utf8NoBOM
$result | ConvertTo-Json
