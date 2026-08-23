[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$SourceRoot = 'D:\UNITY2\Cow',

    [Parameter(Mandatory = $false)]
    [string]$TargetRoot = 'D:\UNITY\Company War-RE',

    [Parameter(Mandatory = $false)]
    [string]$SnapshotId = '20260823-first-batch-v2'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

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

New-Item -ItemType Directory -Path $snapshotDir | Out-Null

$historyBundle = Join-Path $snapshotDir 'Cow-history.bundle'
$workingTreeArchive = Join-Path $snapshotDir 'Cow-working-tree.zip'
$fileManifest = Join-Path $snapshotDir 'Cow-working-tree-files.csv'
$gitStatusPath = Join-Path $snapshotDir 'git-status-porcelain-v2.txt'
$gitHeadPath = Join-Path $snapshotDir 'git-head-and-refs.txt'
$deletionManifestPath = Join-Path $snapshotDir 'tracked-deletions.txt'
$restoreReportPath = Join-Path $snapshotDir 'restore-verification.txt'

$authoringRoots = @('Assets', 'Packages', 'ProjectSettings', 'Tools', '.agents')
$rootFiles = Get-ChildItem -LiteralPath $source -Force -File |
    Where-Object { $_.Name -notin @('Cow.zip') }

$files = [System.Collections.Generic.List[System.IO.FileInfo]]::new()
foreach ($relativeRoot in $authoringRoots) {
    $absoluteRoot = Join-Path $source $relativeRoot
    if (Test-Path -LiteralPath $absoluteRoot) {
        Get-ChildItem -LiteralPath $absoluteRoot -Recurse -Force -File | ForEach-Object { $files.Add($_) }
    }
}
$rootFiles | ForEach-Object { $files.Add($_) }
$files = $files | Sort-Object FullName -Unique

$manifestRows = foreach ($file in $files) {
    $relativePath = $file.FullName.Substring($source.Length + 1).Replace('\', '/')
    [pscustomobject]@{
        Path = $relativePath
        Bytes = $file.Length
        LastWriteTimeUtc = $file.LastWriteTimeUtc.ToString('o')
        SHA256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}
$manifestRows | Export-Csv -LiteralPath $fileManifest -NoTypeInformation -Encoding UTF8

$status = git --no-optional-locks -C $source status --porcelain=v2 --branch --untracked-files=all
[System.IO.File]::WriteAllLines($gitStatusPath, [string[]]$status, [System.Text.UTF8Encoding]::new($false))

$headAndRefs = @(
    "HEAD=$(git --no-optional-locks -C $source rev-parse HEAD)"
    "HEAD_DESCRIPTION=$(git --no-optional-locks -C $source describe --always --dirty --broken)"
    'REFS:'
) + @(git --no-optional-locks -C $source show-ref)
[System.IO.File]::WriteAllLines($gitHeadPath, [string[]]$headAndRefs, [System.Text.UTF8Encoding]::new($false))

$deleted = git --no-optional-locks -C $source ls-files --deleted
[System.IO.File]::WriteAllLines($deletionManifestPath, [string[]]$deleted, [System.Text.UTF8Encoding]::new($false))

git --no-optional-locks -C $source bundle create $historyBundle --all
if ($LASTEXITCODE -ne 0) { throw 'git bundle creation failed.' }
git bundle verify $historyBundle | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'git bundle verification failed.' }

$zip = [System.IO.Compression.ZipFile]::Open($workingTreeArchive, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($row in $manifestRows) {
        $sourceFile = Join-Path $source $row.Path.Replace('/', '\')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $zip,
            $sourceFile,
            $row.Path,
            [System.IO.Compression.CompressionLevel]::Optimal
        ) | Out-Null
    }
}
finally {
    $zip.Dispose()
}

$verificationRoot = Join-Path $target "Temp\CowSnapshotVerification-$SnapshotId"
Assert-ChildPath -Parent $target -Child $verificationRoot
if (Test-Path -LiteralPath $verificationRoot) {
    throw "Verification directory already exists; refusing to overwrite: $verificationRoot"
}
New-Item -ItemType Directory -Path $verificationRoot | Out-Null

$verificationErrors = [System.Collections.Generic.List[string]]::new()
try {
    Expand-Archive -LiteralPath $workingTreeArchive -DestinationPath $verificationRoot
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
    $resolvedVerification = [System.IO.Path]::GetFullPath($verificationRoot)
    Assert-ChildPath -Parent (Join-Path $target 'Temp') -Child $resolvedVerification
    if (Test-Path -LiteralPath $resolvedVerification) {
        Remove-Item -LiteralPath $resolvedVerification -Recurse -Force
    }
}

if ($verificationErrors.Count -gt 0) {
    [System.IO.File]::WriteAllLines($restoreReportPath, $verificationErrors, [System.Text.UTF8Encoding]::new($false))
    throw "Snapshot restore verification failed with $($verificationErrors.Count) error(s)."
}

$archiveHash = (Get-FileHash -LiteralPath $workingTreeArchive -Algorithm SHA256).Hash.ToLowerInvariant()
$bundleHash = (Get-FileHash -LiteralPath $historyBundle -Algorithm SHA256).Hash.ToLowerInvariant()
$report = @(
    'Cow recoverable snapshot verification: PASS'
    "SnapshotId: $SnapshotId"
    "SourceRoot: $source"
    "SourceWasReadOnlyByPolicy: true"
    "GitBundleVerified: true"
    "WorkingTreeRestoreVerified: true"
    "FileCount: $($manifestRows.Count)"
    "WorkingTreeArchiveSHA256: $archiveHash"
    "GitBundleSHA256: $bundleHash"
    'ExcludedGeneratedRoots: Library, Logs, UserSettings, Temp, obj, standalone build directory Cow'
)
[System.IO.File]::WriteAllLines($restoreReportPath, $report, [System.Text.UTF8Encoding]::new($false))

$snapshotFiles = Get-ChildItem -LiteralPath $snapshotDir -File | Sort-Object Name
$checksumLines = foreach ($file in $snapshotFiles) {
    "{0} *{1}" -f (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $file.Name
}
[System.IO.File]::WriteAllLines((Join-Path $snapshotDir 'SHA256SUMS'), $checksumLines, [System.Text.UTF8Encoding]::new($false))

Write-Output "Snapshot created and restore-verified: $snapshotDir"
