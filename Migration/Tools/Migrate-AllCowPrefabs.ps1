[CmdletBinding()]
param(
    [string]$SourceRoot = 'D:\UNITY2\Cow',
    [string]$TargetRoot = 'D:\UNITY\Company War-RE',
    [string]$SnapshotId = '20260826-all-prefabs-batch-05'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Assert-ChildPath([string]$Parent, [string]$Child) {
    $resolvedParent = [IO.Path]::GetFullPath($Parent).TrimEnd('\') + '\'
    $resolvedChild = [IO.Path]::GetFullPath($Child)
    if (-not $resolvedChild.StartsWith($resolvedParent, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing path outside intended parent: $resolvedChild"
    }
}

function Read-AssetGuid([string]$MetaPath) {
    if (-not (Test-Path -LiteralPath $MetaPath -PathType Leaf)) { return '' }
    foreach ($line in Get-Content -LiteralPath $MetaPath -TotalCount 6) {
        if ($line -match '^guid: ([0-9a-f]{32})$') { return $Matches[1] }
    }
    return ''
}

function Find-GuidReferences([string]$Path) {
    $results = [Collections.Generic.List[string]]::new()
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $results }
    try { $text = [IO.File]::ReadAllText($Path) } catch { return $results }
    foreach ($match in [regex]::Matches($text, 'guid: ([0-9a-f]{32})')) {
        if (-not $results.Contains($match.Groups[1].Value)) { $results.Add($match.Groups[1].Value) }
    }
    return $results
}

$source = [IO.Path]::GetFullPath($SourceRoot).TrimEnd('\')
$target = [IO.Path]::GetFullPath($TargetRoot).TrimEnd('\')
$sourceAssets = Join-Path $source 'Assets'
$targetAssets = Join-Path $target 'Assets'
$targetResourceRoot = Join-Path $target 'Assets\CompanyWarRE\Resources\CowLegacy'
$snapshotRoot = Join-Path $target "Migration\Baseline\Cow\$SnapshotId"
$inventoryRoot = Join-Path $target 'Migration\Inventory\PrefabMigrationBatch05'

Assert-ChildPath $source $sourceAssets
Assert-ChildPath $target $targetResourceRoot
Assert-ChildPath $target $snapshotRoot
Assert-ChildPath $target $inventoryRoot
if (-not (Test-Path -LiteralPath (Join-Path $source '.git'))) {
    throw "Cow source is not a Git worktree: $source"
}
if (Test-Path -LiteralPath $snapshotRoot) {
    throw "Snapshot already exists; refusing to overwrite: $snapshotRoot"
}

$sourceGuidToAsset = @{}
Get-ChildItem -LiteralPath $sourceAssets -Recurse -File -Filter '*.meta' | ForEach-Object {
    $guid = Read-AssetGuid $_.FullName
    $assetPath = $_.FullName.Substring(0, $_.FullName.Length - 5)
    if ($guid -and (Test-Path -LiteralPath $assetPath -PathType Leaf)) {
        $sourceGuidToAsset[$guid] = $assetPath
    }
}

$targetGuidToAsset = @{}
Get-ChildItem -LiteralPath $targetAssets -Recurse -File -Filter '*.meta' | ForEach-Object {
    $guid = Read-AssetGuid $_.FullName
    $assetPath = $_.FullName.Substring(0, $_.FullName.Length - 5)
    if ($guid -and (Test-Path -LiteralPath $assetPath -PathType Leaf) -and
        -not $targetGuidToAsset.ContainsKey($guid)) {
        $targetGuidToAsset[$guid] = $assetPath
    }
}

$compatibilityTargets = @{
    'e52691e61cc9cfb4eb29e48a69f4f231' = 'Assets/CompanyWarRE/Compatibility/CowUI/MenuPanel.cs'
    '81c4b70ccd8a8b94bbc0f6eff40736f7' = 'Assets/CompanyWarRE/Compatibility/CowUI/LevelSelectPanel.cs'
    '642b2c2954fd5574fa229fca7cac5ac4' = 'Assets/CompanyWarRE/Compatibility/CowUI/BattlePanel.cs'
    '0411f205fe34b6e4aa5bc9e62ecaec3c' = 'Assets/CompanyWarRE/Compatibility/CowUI/FloatingTextItem.cs'
}

$queue = [Collections.Generic.Queue[string]]::new()
$prefabSources = Get-ChildItem -LiteralPath $sourceAssets -Recurse -File -Filter '*.prefab' |
    Sort-Object FullName
foreach ($prefab in $prefabSources) { $queue.Enqueue($prefab.FullName) }

$closure = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
while ($queue.Count -gt 0) {
    $assetPath = $queue.Dequeue()
    if (-not $closure.Add($assetPath)) { continue }
    foreach ($parsePath in @($assetPath, ($assetPath + '.meta'))) {
        foreach ($guid in Find-GuidReferences $parsePath) {
            if ($sourceGuidToAsset.ContainsKey($guid)) {
                $dependency = $sourceGuidToAsset[$guid]
                if (-not $closure.Contains($dependency)) { $queue.Enqueue($dependency) }
            }
        }
    }
}

New-Item -ItemType Directory -Path $snapshotRoot -Force | Out-Null
New-Item -ItemType Directory -Path $inventoryRoot -Force | Out-Null
$snapshotManifest = [Collections.Generic.List[object]]::new()
foreach ($assetPath in $closure | Sort-Object) {
    foreach ($filePath in @($assetPath, ($assetPath + '.meta'))) {
        if (-not (Test-Path -LiteralPath $filePath -PathType Leaf)) { continue }
        $relative = $filePath.Substring($source.Length + 1).Replace('\', '/')
        $item = Get-Item -LiteralPath $filePath
        $snapshotManifest.Add([pscustomobject]@{
            Path = $relative
            Bytes = $item.Length
            SHA256 = (Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash.ToLowerInvariant()
        })
    }
}

$snapshotManifestPath = Join-Path $snapshotRoot 'all-prefab-resource-files.csv'
$snapshotManifest | Export-Csv -LiteralPath $snapshotManifestPath -NoTypeInformation -Encoding UTF8
$archivePath = Join-Path $snapshotRoot 'all-prefab-resources.zip'
$archive = [IO.Compression.ZipFile]::Open($archivePath, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($row in $snapshotManifest) {
        $absolute = Join-Path $source $row.Path.Replace('/', '\')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive, $absolute, $row.Path, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally { $archive.Dispose() }

$verifyRoot = Join-Path $target "Temp\CowPrefabSnapshotVerification-$SnapshotId"
Assert-ChildPath (Join-Path $target 'Temp') $verifyRoot
try {
    [IO.Compression.ZipFile]::ExtractToDirectory($archivePath, $verifyRoot)
    foreach ($row in $snapshotManifest) {
        $restored = Join-Path $verifyRoot $row.Path.Replace('/', '\')
        if (-not (Test-Path -LiteralPath $restored -PathType Leaf)) {
            throw "Snapshot restore is missing $($row.Path)"
        }
        $hash = (Get-FileHash -LiteralPath $restored -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($hash -ne $row.SHA256) { throw "Snapshot hash mismatch: $($row.Path)" }
    }
}
finally {
    if (Test-Path -LiteralPath $verifyRoot) { Remove-Item -LiteralPath $verifyRoot -Recurse -Force }
}

$migrationRows = [Collections.Generic.List[object]]::new()
foreach ($assetPath in $closure | Sort-Object) {
    $sourceRelative = $assetPath.Substring($source.Length + 1).Replace('\', '/')
    $sourceGuid = Read-AssetGuid ($assetPath + '.meta')
    $extension = [IO.Path]::GetExtension($assetPath).ToLowerInvariant()
    $targetRelative = ''
    $status = ''
    if ($extension -eq '.cs') {
        if (-not $compatibilityTargets.ContainsKey($sourceGuid)) {
            throw "Unexpected Cow script in prefab dependency closure: $sourceRelative"
        }
        $targetRelative = $compatibilityTargets[$sourceGuid]
        $status = 'CompatibilityShim'
    }
    elseif ($sourceGuid -and $targetGuidToAsset.ContainsKey($sourceGuid)) {
        $targetRelative = $targetGuidToAsset[$sourceGuid].Substring($target.Length + 1).Replace('\', '/')
        $status = 'ExistingGuidReused'
    }
    else {
        $insideAssets = $sourceRelative.Substring('Assets/'.Length)
        $targetRelative = 'Assets/CompanyWarRE/Resources/CowLegacy/' + $insideAssets
        $destination = Join-Path $target $targetRelative.Replace('/', '\')
        Assert-ChildPath $targetResourceRoot $destination
        if (Test-Path -LiteralPath $destination) {
            throw "Target asset already exists; refusing to overwrite: $targetRelative"
        }
        New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
        Copy-Item -LiteralPath $assetPath -Destination $destination
        Copy-Item -LiteralPath ($assetPath + '.meta') -Destination ($destination + '.meta')
        $status = 'CopiedWithMeta'
    }

    $migrationRows.Add([pscustomobject]@{
        SourcePath = $sourceRelative
        SourceGuid = $sourceGuid
        TargetPath = $targetRelative
        AssetKind = $(if ($extension -eq '.prefab') { 'Prefab' } else { 'Dependency' })
        Status = $status
        SourceSHA256 = (Get-FileHash -LiteralPath $assetPath -Algorithm SHA256).Hash.ToLowerInvariant()
    })
}

$migrationRows | Export-Csv -LiteralPath (Join-Path $inventoryRoot 'AllPrefabMigrationManifest.csv') -NoTypeInformation -Encoding UTF8
$dependencyRows = foreach ($prefab in $prefabSources) {
    $prefabRelative = $prefab.FullName.Substring($source.Length + 1).Replace('\', '/')
    foreach ($guid in Find-GuidReferences $prefab.FullName) {
        [pscustomobject]@{
            PrefabSourcePath = $prefabRelative
            DependencyGuid = $guid
            DependencySourcePath = $(if ($sourceGuidToAsset.ContainsKey($guid)) {
                $sourceGuidToAsset[$guid].Substring($source.Length + 1).Replace('\', '/')
            } else { '<package-or-built-in>' })
        }
    }
}
$dependencyRows | Export-Csv -LiteralPath (Join-Path $inventoryRoot 'PrefabDirectDependencies.csv') -NoTypeInformation -Encoding UTF8

$verification = @(
    'Cow all-prefab snapshot restore verification: PASS',
    "SnapshotId: $SnapshotId",
    "PrefabCount: $($prefabSources.Count)",
    "AssetClosureCount: $($closure.Count)",
    "SnapshotFileCount: $($snapshotManifest.Count)",
    'SourceWasReadOnlyByPolicy: true',
    'ArchiveRestoreVerified: true',
    "ArchiveSHA256: $((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant())"
)
[IO.File]::WriteAllLines(
    (Join-Path $snapshotRoot 'restore-verification.txt'),
    $verification,
    [Text.UTF8Encoding]::new($false))

$checksumLines = Get-ChildItem -LiteralPath $snapshotRoot -File | Sort-Object Name | ForEach-Object {
    "{0} *{1}" -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name
}
[IO.File]::WriteAllLines(
    (Join-Path $snapshotRoot 'SHA256SUMS'),
    $checksumLines,
    [Text.UTF8Encoding]::new($false))

Write-Output "Migrated $($prefabSources.Count) Cow prefabs through a $($closure.Count)-asset dependency closure."
Write-Output "Snapshot: $snapshotRoot"
Write-Output "Inventory: $inventoryRoot"
