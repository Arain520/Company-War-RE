[CmdletBinding()]
param([string]$TargetRoot = 'D:\UNITY\Company War-RE')

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$target = [IO.Path]::GetFullPath($TargetRoot).TrimEnd('\')
$roots = @(
    (Join-Path $target 'Assets\CompanyWarRE\Resources'),
    (Join-Path $target 'Assets\CompanyWarRE\Compatibility')
)
$created = 0
foreach ($root in $roots) {
    if (-not (Test-Path -LiteralPath $root -PathType Container)) { continue }
    foreach ($directory in @((Get-Item -LiteralPath $root)) + @(Get-ChildItem -LiteralPath $root -Recurse -Directory)) {
        $metaPath = $directory.FullName + '.meta'
        if (Test-Path -LiteralPath $metaPath -PathType Leaf) { continue }
        $relative = $directory.FullName.Substring($target.Length + 1).Replace('\', '/').ToLowerInvariant()
        $bytes = [Text.Encoding]::UTF8.GetBytes('companywarre-folder:' + $relative)
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $guid = ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant().Substring(0, 32) }
        finally { $sha.Dispose() }
        $content = @(
            'fileFormatVersion: 2',
            "guid: $guid",
            'folderAsset: yes',
            'DefaultImporter:',
            '  externalObjects: {}',
            '  userData:',
            '  assetBundleName:',
            '  assetBundleVariant:'
        )
        [IO.File]::WriteAllLines($metaPath, $content, [Text.UTF8Encoding]::new($false))
        $created++
    }
}

Write-Output "Created $created deterministic folder meta files for migrated target assets."
