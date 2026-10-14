param([string]$OutputFile = '')
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot -Parent
if (-not (Test-Path -LiteralPath (Join-Path $repositoryRoot 'LICENSE') -PathType Leaf)) {
    throw 'Select and add the project LICENSE before exporting the public source.'
}
& git -C $repositoryRoot rev-parse --is-inside-work-tree | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Initialize a Git repository and stage the reviewed public files first.' }
$tracked = @(& git -C $repositoryRoot -c core.quotepath=false ls-files --cached)
if ($LASTEXITCODE -ne 0 -or $tracked.Count -eq 0 -or $tracked -notcontains 'LICENSE') {
    throw 'Stage the reviewed public files, including LICENSE, before exporting.'
}
$forbidden = @($tracked | Where-Object {
    $_ -match '(^|/)(artifacts|bin|obj|publish|archive|generated-icons|\.aws|\.codex|\.agents)(/|$)' -or
    $_ -match '(?i)\.(exe|dll|pdb|pfx|p12|key|lnk|log|dmp|zip|7z)$' -or
    $_ -match '(^|/)(backups\.json|\.env)(\.|$)' -or
    $_ -match '(^|/)(AGENTS\.md|작업 인계\.md|시스템 아이콘 구현과 검증\.md|백업 위치\.txt|지시사항[^/]*\.md)$'
})
if ($forbidden.Count -gt 0) { throw ('Private/generated files are staged: ' + ($forbidden -join ', ')) }
$project = Join-Path $repositoryRoot 'LIBENS IconChanger\essential\source\LIBENS IconChanger.csproj'
[xml]$projectXml = Get-Content -LiteralPath $project -Raw
$version = [string]$projectXml.Project.PropertyGroup.Version
$archive = if ($OutputFile) { [IO.Path]::GetFullPath($OutputFile, (Get-Location).ProviderPath) } else {
    Join-Path $repositoryRoot "LIBENS IconChanger\artifacts\github\LIBENS-IconChanger-$version-source.zip"
}
if (Test-Path -LiteralPath $archive) { throw "Refusing to overwrite an existing archive: $archive" }
New-Item -ItemType Directory -Path (Split-Path $archive -Parent) -Force | Out-Null
$tree = (& git -C $repositoryRoot write-tree).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Could not snapshot the Git index.' }
& git -C $repositoryRoot archive --format=zip --prefix=LIBENS-IconChanger-source/ "--output=$archive" $tree
if ($LASTEXITCODE -ne 0) { throw 'Source archive export failed.' }
Get-Item -LiteralPath $archive | Select-Object FullName, Length
