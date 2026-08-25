[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$SourceRoot = 'D:\UNITY2\Cow',

    [Parameter(Mandatory = $false)]
    [string]$TargetRoot = 'D:\UNITY\Company War-RE',

    [Parameter(Mandatory = $false)]
    [string]$SnapshotId = '20260825-production-resources-batch-01'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Assert-ChildPath {
    param(
        [Parameter(Mandatory = $true)][string]$Parent,
        [Parameter(Mandatory = $true)][string]$Child
    )

    $resolvedParent = [System.IO.Path]::GetFullPath($Parent).TrimEnd('\') + '\'
    $resolvedChild = [System.IO.Path]::GetFullPath($Child)
    if (-not $resolvedChild.StartsWith($resolvedParent, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing path outside intended parent: $resolvedChild"
    }
}

$source = [System.IO.Path]::GetFullPath($SourceRoot).TrimEnd('\')
$target = [System.IO.Path]::GetFullPath($TargetRoot).TrimEnd('\')
$snapshotDir = Join-Path $target "Migration\Baseline\Cow\$SnapshotId"
Assert-ChildPath -Parent $target -Child $snapshotDir

if (-not (Test-Path -LiteralPath (Join-Path $source '.git'))) {
    throw "Cow source is not a Git worktree: $source"
}
if (Test-Path -LiteralPath $snapshotDir) {
    throw "Snapshot directory already exists; refusing to overwrite: $snapshotDir"
}

$selectedAssets = @(
    'Assets/_Game/Art/Prefabs/Units/PF_U01.prefab',
    'Assets/_Game/Art/Prefabs/Enemies/PF_E01.prefab',
    'Assets/_Game/Art/Prefabs/Buildings/PF_E06.prefab',
    'Assets/_Game/Art/Prefabs/Buildings/PF_E07.prefab',
    'Assets/_Game/Art/Materials/Mat_U01.mat',
    'Assets/_Game/Art/Materials/Mat_E01.mat',
    'Assets/_Game/Art/Materials/Mat_E06.mat',
    'Assets/_Game/Art/Materials/Mat_E07.mat',
    'Assets/_Game/Art/Prefabs/Units/U01.obj',
    'Assets/_Game/Resources/11111/1.fbx',
    'Assets/_Game/Resources/11111/6.fbx',
    'Assets/_Game/Resources/11111/7.fbx',
    'Assets/_Game/Resources/VisualMapping.asset'
)

$selectedFiles = [System.Collections.Generic.List[string]]::new()
foreach ($relativePath in $selectedAssets) {
    foreach ($candidate in @($relativePath, ($relativePath + '.meta'))) {
        $absolutePath = Join-Path $source $candidate.Replace('/', '\')
        if (-not (Test-Path -LiteralPath $absolutePath -PathType Leaf)) {
            throw "Selected Cow resource is missing: $candidate"
        }
        $selectedFiles.Add($candidate)
    }
}

New-Item -ItemType Directory -Path $snapshotDir | Out-Null
$archivePath = Join-Path $snapshotDir 'selected-resources.zip'
$manifestPath = Join-Path $snapshotDir 'selected-resource-files.csv'
$statusPath = Join-Path $snapshotDir 'git-status-selected.txt'
$verificationPath = Join-Path $snapshotDir 'restore-verification.txt'

$manifestRows = foreach ($relativePath in $selectedFiles) {
    $absolutePath = Join-Path $source $relativePath.Replace('/', '\')
    $file = Get-Item -LiteralPath $absolutePath
    [pscustomobject]@{
        Path = $relativePath
        Bytes = $file.Length
        SHA256 = (Get-FileHash -LiteralPath $absolutePath -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}
$manifestRows | Export-Csv -LiteralPath $manifestPath -NoTypeInformation -Encoding UTF8

$status = git --no-optional-locks -C $source status --porcelain=v1 -- @selectedAssets
[System.IO.File]::WriteAllLines($statusPath, [string[]]$status, [System.Text.UTF8Encoding]::new($false))

$zip = [System.IO.Compression.ZipFile]::Open($archivePath, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($row in $manifestRows) {
        $absolutePath = Join-Path $source $row.Path.Replace('/', '\')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $zip,
            $absolutePath,
            $row.Path,
            [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally {
    $zip.Dispose()
}

$verificationRoot = Join-Path $target "Temp\CowResourceSnapshotVerification-$SnapshotId"
Assert-ChildPath -Parent (Join-Path $target 'Temp') -Child $verificationRoot
if (Test-Path -LiteralPath $verificationRoot) {
    throw "Verification directory already exists; refusing to overwrite: $verificationRoot"
}

$verificationErrors = [System.Collections.Generic.List[string]]::new()
try {
    [System.IO.Compression.ZipFile]::ExtractToDirectory($archivePath, $verificationRoot)
    foreach ($row in $manifestRows) {
        $restoredPath = Join-Path $verificationRoot $row.Path.Replace('/', '\')
        if (-not (Test-Path -LiteralPath $restoredPath -PathType Leaf)) {
            $verificationErrors.Add("MISSING $($row.Path)")
            continue
        }

        $restoredHash = (Get-FileHash -LiteralPath $restoredPath -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($restoredHash -ne $row.SHA256) {
            $verificationErrors.Add("HASH_MISMATCH $($row.Path)")
        }
    }
}
finally {
    if (Test-Path -LiteralPath $verificationRoot) {
        Remove-Item -LiteralPath $verificationRoot -Recurse -Force
    }
}

if ($verificationErrors.Count -gt 0) {
    [System.IO.File]::WriteAllLines($verificationPath, $verificationErrors, [System.Text.UTF8Encoding]::new($false))
    throw "Selected resource snapshot verification failed with $($verificationErrors.Count) error(s)."
}

$report = @(
    'Cow selected resource snapshot verification: PASS',
    "SnapshotId: $SnapshotId",
    "SourceRoot: $source",
    'SourceWasReadOnlyByPolicy: true',
    "FileCount: $($manifestRows.Count)",
    'ArchiveRestoreVerified: true',
    "ArchiveSHA256: $((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant())"
)
[System.IO.File]::WriteAllLines($verificationPath, $report, [System.Text.UTF8Encoding]::new($false))

$checksumLines = Get-ChildItem -LiteralPath $snapshotDir -File | Sort-Object Name | ForEach-Object {
    "{0} *{1}" -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name
}
[System.IO.File]::WriteAllLines(
    (Join-Path $snapshotDir 'SHA256SUMS'),
    $checksumLines,
    [System.Text.UTF8Encoding]::new($false))

Write-Output "Selected Cow resource snapshot created and restore-verified: $snapshotDir"
