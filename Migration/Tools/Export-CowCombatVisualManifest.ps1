[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$SourceRoot = 'D:\UNITY2\Cow',

    [Parameter(Mandatory = $false)]
    [string]$TargetRoot = 'D:\UNITY\Company War-RE'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$source = [System.IO.Path]::GetFullPath($SourceRoot).TrimEnd('\')
$target = [System.IO.Path]::GetFullPath($TargetRoot).TrimEnd('\')
$mappingPath = Join-Path $source 'Assets\_Game\Resources\VisualMapping.asset'
$outputPath = Join-Path $target 'Migration\Inventory\ProductionResources\CombatVisualResourceMap.csv'

if (-not (Test-Path -LiteralPath $mappingPath -PathType Leaf)) {
    throw "Cow VisualMapping asset was not found: $mappingPath"
}

$guidToPath = @{}
Get-ChildItem -LiteralPath (Join-Path $source 'Assets') -Recurse -Filter '*.meta' -File | ForEach-Object {
    $guidLine = Get-Content -LiteralPath $_.FullName -TotalCount 4 |
        Where-Object { $_ -match '^guid: ([0-9a-f]{32})$' } |
        Select-Object -First 1
    if ($guidLine -and $guidLine -match '^guid: ([0-9a-f]{32})$') {
        $assetPath = $_.FullName.Substring($source.Length + 1, $_.FullName.Length - $source.Length - 6)
        $guidToPath[$Matches[1]] = $assetPath.Replace('\', '/')
    }
}

function Resolve-PathFromGuid([string]$Guid) {
    if ([string]::IsNullOrWhiteSpace($Guid) -or -not $guidToPath.ContainsKey($Guid)) {
        return ''
    }
    return $guidToPath[$Guid]
}

$rows = [System.Collections.Generic.List[object]]::new()
$kind = ''
$current = $null
foreach ($line in Get-Content -LiteralPath $mappingPath) {
    if ($line -eq '  unitMappings:') {
        $kind = 'Unit'
        continue
    }
    if ($line -eq '  enemyMappings:') {
        $kind = 'Enemy'
        continue
    }
    if ($line -match '^  - (unitId|enemyId): (\S+)$') {
        if ($null -ne $current) {
            $rows.Add($current)
        }
        $templateId = $Matches[2]
        $current = [ordered]@{
            TemplateId = $templateId
            Kind = $kind
            PrefabGuid = ''
            PrefabSourcePath = ''
            MaterialGuid = ''
            MaterialSourcePath = ''
            ModelGuid = ''
            ModelSourcePath = ''
            TargetStatus = $(if ($templateId -in @('U01', 'E01', 'E06', 'E07')) { 'ImportedBatch01' } else { 'Pending' })
            Notes = $(if ($templateId -in @('E06', 'E07')) { 'Enemy building visual' } else { '' })
        }
        continue
    }
    if ($null -eq $current) {
        continue
    }
    if ($line -match '^    (prefab|material|model): \{fileID: ([-0-9]+)(?:, guid: ([0-9a-f]{32}), type: [0-9]+)?\}$') {
        $slot = $Matches[1]
        $guid = $Matches[3]
        if ($slot -eq 'prefab') {
            $current.PrefabGuid = $guid
            $current.PrefabSourcePath = Resolve-PathFromGuid $guid
        }
        elseif ($slot -eq 'material') {
            $current.MaterialGuid = $guid
            $current.MaterialSourcePath = Resolve-PathFromGuid $guid
        }
        else {
            $current.ModelGuid = $guid
            $current.ModelSourcePath = Resolve-PathFromGuid $guid
        }
    }
}
if ($null -ne $current) {
    $rows.Add($current)
}

if ($rows.Count -ne 51) {
    throw "Expected 51 Cow visual mappings but parsed $($rows.Count)."
}

$outputDirectory = Split-Path -Parent $outputPath
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$rows | ForEach-Object { [pscustomobject]$_ } |
    Export-Csv -LiteralPath $outputPath -NoTypeInformation -Encoding UTF8

Write-Output "Exported $($rows.Count) Cow combat visual mappings: $outputPath"
