[CmdletBinding()]
param([string]$TargetRoot = 'D:\UNITY\Company War-RE')

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$target = [IO.Path]::GetFullPath($TargetRoot).TrimEnd('\')
$materialRoot = Join-Path $target 'Assets\CompanyWarRE\Resources\CowLegacy'
$templatePath = Join-Path $target 'Assets\CompanyWarRE\Content\Materials\Mat_U01.mat'
if (-not (Test-Path -LiteralPath $templatePath -PathType Leaf)) {
    throw "URP material template is missing: $templatePath"
}

$template = [IO.File]::ReadAllText($templatePath)
$converted = 0
foreach ($material in Get-ChildItem -LiteralPath $materialRoot -Recurse -File -Filter '*.mat') {
    $source = [IO.File]::ReadAllText($material.FullName)
    $nameMatch = [regex]::Match($source, '(?m)^  m_Name: (.+)$')
    $colorMatch = [regex]::Match($source, '(?m)^    - _Color: (.+)$')
    $smoothnessMatch = [regex]::Match($source, '(?m)^    - _Glossiness: (.+)$')
    if (-not $nameMatch.Success -or -not $colorMatch.Success) {
        throw "Cannot read legacy material name/color: $($material.FullName)"
    }

    $result = [regex]::Replace($template, '(?m)^  m_Name: .+$', '  m_Name: ' + $nameMatch.Groups[1].Value, 1)
    $result = [regex]::Replace($result, '(?m)^    - _Color: .+$', '    - _Color: ' + $colorMatch.Groups[1].Value, 1)
    $result = [regex]::Replace($result, '(?m)^    - _BaseColor: .+$', '    - _BaseColor: ' + $colorMatch.Groups[1].Value, 1)
    if ($smoothnessMatch.Success) {
        $result = [regex]::Replace(
            $result,
            '(?m)^    - _Smoothness: .+$',
            '    - _Smoothness: ' + $smoothnessMatch.Groups[1].Value,
            1)
    }

    [IO.File]::WriteAllText($material.FullName, $result, [Text.UTF8Encoding]::new($false))
    $converted++
}

Write-Output "Converted $converted migrated Cow materials to URP/Lit while preserving material meta GUIDs."
