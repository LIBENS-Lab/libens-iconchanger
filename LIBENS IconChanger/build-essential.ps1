param(
    [string]$OutputDirectory = '',
    [ValidatePattern('^10\.0\.\d+$')]
    [string]$RuntimeFrameworkVersion = '10.0.13'
)
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'essential\source\LIBENS IconChanger.csproj'
$destination = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory, (Get-Location).ProviderPath) } else { Join-Path $PSScriptRoot 'essential' }
$staging = Join-Path $PSScriptRoot ('artifacts\essential-build-' + [Guid]::NewGuid().ToString('N'))
# Publishing into the source's parent causes SDK default globs to exclude the source.
# Build outside essential, then copy only the completed runtime output into it.
& dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -p:DebugType=embedded "-p:RuntimeFrameworkVersion=$RuntimeFrameworkVersion" -o $staging
if ($LASTEXITCODE -ne 0) { throw "essential 빌드 실패: $LASTEXITCODE" }
New-Item -ItemType Directory -Path $destination -Force | Out-Null
foreach ($outputFile in Get-ChildItem -LiteralPath $staging -Recurse -File) {
    $relative = [IO.Path]::GetRelativePath($staging, $outputFile.FullName)
    $targetFile = Join-Path $destination $relative
    if (Test-Path -LiteralPath $targetFile -PathType Leaf) {
        $existing = Get-Item -LiteralPath $targetFile
        if ($existing.Length -eq $outputFile.Length -and
            (Get-FileHash -LiteralPath $targetFile -Algorithm SHA256).Hash -eq
            (Get-FileHash -LiteralPath $outputFile.FullName -Algorithm SHA256).Hash) { continue }
    }
    New-Item -ItemType Directory -Path (Split-Path $targetFile -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $outputFile.FullName -Destination $targetFile -Force
}
$restore = Get-Content (Join-Path $PSScriptRoot 'artifacts\obj\LIBENS IconChanger\project.assets.json') -Raw | ConvertFrom-Json
$runtime = Get-Content (Join-Path $destination 'LIBENS IconChanger.runtimeconfig.json') -Raw | ConvertFrom-Json
foreach ($framework in $runtime.runtimeOptions.includedFrameworks) {
    $packageId = $framework.name.ToLowerInvariant() + '.runtime.win-x64'
    $packageRoot = $null
    foreach ($folder in $restore.packageFolders.PSObject.Properties.Name) {
        $candidate = Join-Path $folder ($packageId + '\' + $framework.version)
        if (Test-Path -LiteralPath $candidate) { $packageRoot = $candidate; break }
    }
    if (-not $packageRoot) { throw "런타임 고지문 원본을 찾지 못했습니다: $packageId" }
    $noticeFolder = Join-Path $destination ('licenses\' + $framework.name)
    New-Item -ItemType Directory -Force -Path $noticeFolder | Out-Null
    Get-ChildItem -LiteralPath $packageRoot -File | Where-Object { $_.Name -match '^(LICENSE|THIRD-PARTY-NOTICES)(\..*)?$' } | Copy-Item -Destination $noticeFolder -Force
}
if ([IO.Path]::GetFullPath($destination).TrimEnd('\') -ne [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'essential')).TrimEnd('\')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'essential\source') -Destination $destination -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'essential\사용방법.md') -Destination $destination -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'essential\USER-GUIDE.en.md') -Destination $destination -Force
}
$repositoryRoot = Split-Path $PSScriptRoot -Parent
foreach ($document in @('LICENSE', 'THIRD-PARTY-NOTICES.md')) {
    $sourceDocument = Join-Path $repositoryRoot $document
    if (-not (Test-Path -LiteralPath $sourceDocument -PathType Leaf)) { throw "Missing distribution notice: $document" }
    Copy-Item -LiteralPath $sourceDocument -Destination $destination -Force
}
foreach ($file in @('LIBENS IconChanger.exe','LIBENS IconChanger.dll','hostfxr.dll','hostpolicy.dll','coreclr.dll','System.Windows.Forms.dll','assets\app.ico','assets\app.png')) {
    if (-not (Test-Path -LiteralPath (Join-Path $destination $file))) { throw "필수 파일 누락: $file" }
}
Write-Host "실행 파일: $destination\LIBENS IconChanger.exe"
