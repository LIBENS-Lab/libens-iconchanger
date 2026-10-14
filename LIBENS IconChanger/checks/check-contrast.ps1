param([string]$OutputFile = (Join-Path $PSScriptRoot '..\artifacts\contrast.json'))
$ErrorActionPreference = 'Stop'
function Get-Luminance([string]$Hex) {
    $channels = @(1,3,5 | ForEach-Object {
        $value = [Convert]::ToInt32($Hex.Substring($_,2),16) / 255.0
        if ($value -le 0.04045) { $value / 12.92 } else { [Math]::Pow(($value+0.055)/1.055,2.4) }
    })
    return $channels[0]*0.2126 + $channels[1]*0.7152 + $channels[2]*0.0722
}
$pairs = @(
    @('Body', '#F1F3F5', '#16191F', 4.5),
    @('Description', '#BBC1CB', '#16191F', 4.5),
    @('Muted / placeholder', '#9299A5', '#11141A', 4.5),
    @('Muted on card', '#9299A5', '#16191F', 4.5),
    @('Primary button', '#101216', '#54D994', 4.5),
    @('Primary hover', '#101216', '#7CE8AE', 4.5),
    @('Primary pressed', '#101216', '#32BB77', 4.5),
    @('Selected row', '#F1F3F5', '#183E2D', 4.5),
    @('Input border', '#697585', '#11141A', 3),
    @('Secondary border', '#697585', '#1D2129', 3),
    @('Green badge', '#54D994', '#0E1014', 4.5),
    @('Error text', '#FF8EA1', '#0E1014', 4.5),
    @('App icon arrows', '#10271B', '#54D994', 3)
)
$results = foreach ($pair in $pairs) {
    $a = Get-Luminance $pair[1]
    $b = Get-Luminance $pair[2]
    $ratio = ([Math]::Max($a,$b)+0.05)/([Math]::Min($a,$b)+0.05)
    [pscustomobject]@{Name=$pair[0]; Ratio=[Math]::Round($ratio,2); Minimum=$pair[3]; Passed=($ratio -ge $pair[3])}
}
$results | ConvertTo-Json | Set-Content -LiteralPath $OutputFile -Encoding utf8
$results | Format-Table
if ($results.Passed -contains $false) { throw 'Contrast check failed' }
