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

$source = [System.IO.Path]::GetFullPath($SourceRoot).TrimEnd('\')
$target = [System.IO.Path]::GetFullPath($TargetRoot).TrimEnd('\')
$output = Join-Path $target 'Migration\Inventory\FirstBatch'
New-Item -ItemType Directory -Path $output -Force | Out-Null

function Get-RelativeForwardPath {
    param([string]$Root, [string]$Path)
    return $Path.Substring($Root.Length + 1).Replace('\', '/')
}

function Get-MetaGuid {
    param([string]$MetaPath)
    if (-not (Test-Path -LiteralPath $MetaPath)) { return $null }
    $match = Select-String -LiteralPath $MetaPath -Pattern '^guid:\s*([0-9a-fA-F]{32})\s*$' | Select-Object -First 1
    if ($null -eq $match) { return $null }
    return $match.Matches[0].Groups[1].Value.ToLowerInvariant()
}

function Read-PackageManifest {
    param([string]$Project, [string]$ProjectName)
    $manifestPath = Join-Path $Project 'Packages\manifest.json'
    $lockPath = Join-Path $Project 'Packages\packages-lock.json'
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    foreach ($property in $manifest.dependencies.PSObject.Properties) {
        [pscustomobject]@{
            Project = $ProjectName
            Package = $property.Name
            RequestedVersion = [string]$property.Value
            ResolvedVersion = if (Test-Path -LiteralPath $lockPath) {
                $lock = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json
                if ($lock.dependencies.PSObject.Properties.Name -contains $property.Name) {
                    [string]$lock.dependencies.($property.Name).version
                } else { '' }
            } else { '' }
            Depth = 0
            Source = 'Packages/manifest.json'
        }
    }
}

$sourceVersion = Get-Content -LiteralPath (Join-Path $source 'ProjectSettings\ProjectVersion.txt')
$targetVersion = Get-Content -LiteralPath (Join-Path $target 'ProjectSettings\ProjectVersion.txt')
@(
    [pscustomobject]@{ Project='Cow'; Role='Read-only source'; EditorVersion=($sourceVersion[0] -replace '^m_EditorVersion:\s*',''); LockedForMigration=$false }
    [pscustomobject]@{ Project='Company War-RE'; Role='Migration target'; EditorVersion=($targetVersion[0] -replace '^m_EditorVersion:\s*',''); LockedForMigration=$true }
) | Export-Csv -LiteralPath (Join-Path $output 'UnityVersions.csv') -NoTypeInformation -Encoding UTF8

@(Read-PackageManifest -Project $source -ProjectName 'Cow') +
@(Read-PackageManifest -Project $target -ProjectName 'Company War-RE') |
    Sort-Object Project, Package |
    Export-Csv -LiteralPath (Join-Path $output 'Packages.csv') -NoTypeInformation -Encoding UTF8

$thirdParty = @(
    [pscustomobject]@{ Dependency='Addressables'; SourceEvidence='Packages/manifest.json: com.unity.addressables 1.21.20'; TargetEvidence='Not currently requested'; Policy='Compatibility-first; retain/install compatible version when required'; RemovalCommitted=$false }
    [pscustomobject]@{ Dependency='UniTask'; SourceEvidence='Assets plugin and script references'; TargetEvidence='Not currently present'; Policy='Compatibility-first; inventory API usage before any replacement'; RemovalCommitted=$false }
    [pscustomobject]@{ Dependency='DOTween'; SourceEvidence='DOTween DLL/plugin assets'; TargetEvidence='Not currently present'; Policy='Compatibility-first; preserve animation behavior before any replacement'; RemovalCommitted=$false }
    [pscustomobject]@{ Dependency='QFramework'; SourceEvidence='Legacy single-file/framework scripts'; TargetEvidence='QFramework 1.0.257 toolkits'; Policy='Use only at Application/Infrastructure/Presentation boundaries'; RemovalCommitted=$false }
    [pscustomobject]@{ Dependency='HDRP'; SourceEvidence='GraphicsSettings and serialized AdditionalCameraData/AdditionalLightData'; TargetEvidence='URP 14.0.12'; Policy='Resolve serialized HDRP types before renderer conversion; never strip blindly'; RemovalCommitted=$false }
)
$thirdParty | Export-Csv -LiteralPath (Join-Path $output 'DependencyCompatibilityMatrix.csv') -NoTypeInformation -Encoding UTF8

$moduleMappings = @(
    [pscustomobject]@{ SourceModule='Assets/_Game/Scripts/Logic/Core/Domain'; CurrentResponsibility='Battle rules, entities, state transitions'; TargetLayer='Domain'; QFrameworkAllowed=$false; MigrationRule='Pure C# characterization first; no UnityEngine or framework references' }
    [pscustomobject]@{ SourceModule='Assets/_Game/Scripts/Logic/Adapters'; CurrentResponsibility='Logic-to-view bridging and events'; TargetLayer='Application + Presentation'; QFrameworkAllowed=$true; MigrationRule='Split orchestration from Unity-facing view adapters' }
    [pscustomobject]@{ SourceModule='CompanyWarManager / GameFlowManager'; CurrentResponsibility='Bootstrap and scene/game flow'; TargetLayer='Application'; QFrameworkAllowed=$true; MigrationRule='Retain GUID-backed compatibility entry components in Presentation' }
    [pscustomobject]@{ SourceModule='CompanyWarPrototype3DRuntime'; CurrentResponsibility='Battle orchestration, input, camera and view coordination'; TargetLayer='Application + Presentation'; QFrameworkAllowed=$true; MigrationRule='Keep legacy serialization shim, then split by responsibility' }
    [pscustomobject]@{ SourceModule='Grid and corridor renderers'; CurrentResponsibility='Board and corridor visuals'; TargetLayer='Presentation'; QFrameworkAllowed=$true; MigrationRule='Preserve serialized fields/GUIDs before view refactor' }
    [pscustomobject]@{ SourceModule='UI controllers and panel prefabs'; CurrentResponsibility='Menus, HUD and loading UI'; TargetLayer='Presentation'; QFrameworkAllowed=$true; MigrationRule='Compatibility components may delegate to UIKit after parity tests' }
    [pscustomobject]@{ SourceModule='AudioManager'; CurrentResponsibility='Music selection, volume and audio persistence'; TargetLayer='Presentation + Infrastructure'; QFrameworkAllowed=$true; MigrationRule='Preserve PlayerPrefs and field aliases; AudioKit is an adapter option' }
    [pscustomobject]@{ SourceModule='VisualMapping / corridor theme ScriptableObjects'; CurrentResponsibility='Visual configuration'; TargetLayer='Presentation'; QFrameworkAllowed=$true; MigrationRule='Preserve original meta/type/fields; never promote to Domain entity' }
    [pscustomobject]@{ SourceModule='Addressables / StreamingAssets / Resources loading'; CurrentResponsibility='Asset and configuration delivery'; TargetLayer='Infrastructure'; QFrameworkAllowed=$true; MigrationRule='Implement IAssetProvider/IConfigSource; backend remains undecided' }
    [pscustomobject]@{ SourceModule='PlayerPrefs / SaveUtil'; CurrentResponsibility='Progress, stars, audio and dormant encrypted saves'; TargetLayer='Infrastructure'; QFrameworkAllowed=$true; MigrationRule='Implement Domain/Application ports without changing keys or payloads' }
    [pscustomobject]@{ SourceModule='Assets/_Game/Editor'; CurrentResponsibility='Authoring and validation tools'; TargetLayer='Editor tooling'; QFrameworkAllowed=$true; MigrationRule='Migrate only after runtime contracts are approved' }
)
$moduleMappings | Export-Csv -LiteralPath (Join-Path $output 'ModuleMapping.csv') -NoTypeInformation -Encoding UTF8

$guidRows = [System.Collections.Generic.List[object]]::new()
$guidToPath = @{}
$scanRoots = @(
    @{ Root=(Join-Path $source 'Assets'); Kind='Asset' },
    @{ Root=(Join-Path $source 'Packages'); Kind='EmbeddedPackage' },
    @{ Root=(Join-Path $source 'Library\PackageCache'); Kind='PackageCache' }
)
foreach ($scanRoot in $scanRoots) {
    if (-not (Test-Path -LiteralPath $scanRoot.Root)) { continue }
    Get-ChildItem -LiteralPath $scanRoot.Root -Recurse -Force -File -Filter '*.meta' | ForEach-Object {
        $guid = Get-MetaGuid -MetaPath $_.FullName
        if ([string]::IsNullOrWhiteSpace($guid)) { return }
        $assetPath = $_.FullName.Substring(0, $_.FullName.Length - 5)
        $relativePath = if ($assetPath.StartsWith($source, [System.StringComparison]::OrdinalIgnoreCase)) {
            Get-RelativeForwardPath -Root $source -Path $assetPath
        } else { $assetPath.Replace('\','/') }
        if (-not $guidToPath.ContainsKey($guid)) { $guidToPath[$guid] = $relativePath }
        $guidRows.Add([pscustomobject]@{
            Guid=$guid
            AssetPath=$relativePath
            Kind=$scanRoot.Kind
            Extension=[System.IO.Path]::GetExtension($assetPath).ToLowerInvariant()
        })
    }
}
$guidRows | Sort-Object Guid, AssetPath | Export-Csv -LiteralPath (Join-Path $output 'GuidMap.csv') -NoTypeInformation -Encoding UTF8

$assetRows = foreach ($meta in Get-ChildItem -LiteralPath (Join-Path $source 'Assets') -Recurse -Force -File -Filter '*.meta') {
    $assetPath = $meta.FullName.Substring(0, $meta.FullName.Length - 5)
    if (-not (Test-Path -LiteralPath $assetPath -PathType Leaf)) { continue }
    $assetFile = Get-Item -LiteralPath $assetPath
    [pscustomobject]@{
        AssetPath=Get-RelativeForwardPath -Root $source -Path $assetPath
        Guid=Get-MetaGuid -MetaPath $meta.FullName
        Extension=$assetFile.Extension.ToLowerInvariant()
        Bytes=$assetFile.Length
        SHA256=(Get-FileHash -LiteralPath $assetPath -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}
$assetRows | Sort-Object AssetPath | Export-Csv -LiteralPath (Join-Path $output 'AssetInventory.csv') -NoTypeInformation -Encoding UTF8

$referenceExtensions = @('.unity','.prefab','.asset','.mat','.controller','.anim','.overridecontroller','.playable','.preset','.rendertexture','.scenetemplate','.shadergraph','.shadersubgraph')
$referenceRows = [System.Collections.Generic.List[object]]::new()
foreach ($asset in $assetRows | Where-Object { $referenceExtensions -contains $_.Extension }) {
    $absolutePath = Join-Path $source $asset.AssetPath.Replace('/','\')
    try { $content = Get-Content -LiteralPath $absolutePath -Raw -ErrorAction Stop } catch { continue }
    foreach ($match in [regex]::Matches($content, 'guid:\s*([0-9a-fA-F]{32})')) {
        $guid = $match.Groups[1].Value.ToLowerInvariant()
        $resolved = if ($guidToPath.ContainsKey($guid)) { $guidToPath[$guid] } else { '' }
        $referenceRows.Add([pscustomobject]@{
            FromAsset=$asset.AssetPath
            ReferencedGuid=$guid
            ResolvedPath=$resolved
            Resolution=if ($resolved) {'Resolved'} else {'UnresolvedOrBuiltIn'}
        })
    }
}
$uniqueReferenceRows = @($referenceRows | Sort-Object FromAsset, ReferencedGuid -Unique)
$uniqueReferenceRows | Export-Csv -LiteralPath (Join-Path $output 'GuidReferences.csv') -NoTypeInformation -Encoding UTF8

$targetMappings = @{
    'GameFlowManager'='Legacy compatibility component -> Application/CompanyWarFlowController'
    'CompanyWarManager'='Legacy compatibility entry -> Application bootstrap and flow services'
    'CompanyWarPrototype3DRuntime'='LegacyBattleRuntimeCompat -> BattleSessionPresenter + BattleInputController + BattleViewCoordinator'
    'CompanyWarGrid3DRenderer'='Compatibility component -> Presentation/CompanyWarGridView'
    'CorporateWarCorridorFloor'='Compatibility component -> Presentation/CorporateCorridorView'
    'AudioManager'='Preserve initial type -> AudioPresenter + Infrastructure audio gateway'
    'UIChineseFontBinder'='Preserve as Presentation compatibility component'
    'LevelSelectUIController'='Compatibility entry -> UIKit panel/presenter'
    'LoadingScreenController'='Compatibility entry -> UIKit panel/presenter'
    'MainMenuUIController'='Compatibility entry -> UIKit panel/presenter'
    'HudNewsTicker'='Compatibility component -> Presentation ticker view'
    'BattlePanel'='Compatibility panel -> UIKit panel/presenter'
    'LevelSelectPanel'='Compatibility panel -> UIKit panel/presenter'
    'MenuPanel'='Compatibility panel -> UIKit panel/presenter'
    'FloatingTextItem'='Compatibility component -> FloatingTextView/pool adapter'
    'VisualMapping'='Preserve GUID/type/fields as Presentation configuration'
    'CorporateWarCorridorTheme'='Preserve GUID/type/fields as Presentation configuration'
    'HDAdditionalCameraData'='Resolve through compatible HDRP staging before value-level renderer conversion'
    'HDAdditionalLightData'='Resolve through compatible HDRP staging before value-level renderer conversion'
}
$externalGuidTypes = @{
    '23c1ce4fb46143f46bc5cb5224c934f6'='HDAdditionalCameraData'
    '7a68c43fe1f2a47cfa234b5eeaa98012'='HDAdditionalLightData'
    '0cf1dab834d4ec34195b920ea7bbf9ec'='HDRenderPipelineAsset'
    '781cc897cf8675041a751163b51f97dd'='HDRenderPipelineGlobalSettings'
}

$standardMonoFields = @('m_ObjectHideFlags','m_CorrespondingSourceObject','m_PrefabInstance','m_PrefabAsset','m_GameObject','m_Enabled','m_EditorHideFlags','m_Script','m_Name','m_EditorClassIdentifier')
$serializationRows = [System.Collections.Generic.List[object]]::new()
$serializedAssets = $assetRows | Where-Object { $_.Extension -in @('.unity','.prefab','.asset') }
foreach ($asset in $serializedAssets) {
    $absolutePath = Join-Path $source $asset.AssetPath.Replace('/','\')
    try { $lines = Get-Content -LiteralPath $absolutePath -ErrorAction Stop } catch { continue }
    $gameObjectNames = @{}
    $documents = [System.Collections.Generic.List[object]]::new()
    $currentType = $null
    $currentId = $null
    $currentLines = [System.Collections.Generic.List[string]]::new()
    foreach ($line in $lines) {
        if ($line -match '^--- !u!(\d+) &(\-?\d+)') {
            if ($null -ne $currentType) { $documents.Add([pscustomobject]@{Type=$currentType;Id=$currentId;Lines=@($currentLines)}) }
            $currentType = [int]$Matches[1]
            $currentId = $Matches[2]
            $currentLines = [System.Collections.Generic.List[string]]::new()
        } elseif ($null -ne $currentType) {
            $currentLines.Add($line)
        }
    }
    if ($null -ne $currentType) { $documents.Add([pscustomobject]@{Type=$currentType;Id=$currentId;Lines=@($currentLines)}) }

    foreach ($document in $documents | Where-Object { $_.Type -eq 1 }) {
        $nameLine = $document.Lines | Where-Object { $_ -match '^  m_Name:\s*(.*)$' } | Select-Object -First 1
        if ($nameLine -match '^  m_Name:\s*(.*)$') { $gameObjectNames[$document.Id] = $Matches[1] }
    }

    foreach ($document in $documents | Where-Object { $_.Type -eq 114 }) {
        $block = $document.Lines -join "`n"
        $scriptMatch = [regex]::Match($block, 'm_Script:\s*\{[^}]*guid:\s*([0-9a-fA-F]{32})')
        if (-not $scriptMatch.Success) { continue }
        $scriptGuid = $scriptMatch.Groups[1].Value.ToLowerInvariant()
        $scriptPath = if ($guidToPath.ContainsKey($scriptGuid)) { [string]$guidToPath[$scriptGuid] } else { '' }
        $scriptType = if ($scriptPath.EndsWith('.cs', [System.StringComparison]::OrdinalIgnoreCase)) {
            [System.IO.Path]::GetFileNameWithoutExtension($scriptPath)
        } elseif ($externalGuidTypes.ContainsKey($scriptGuid)) {
            $externalGuidTypes[$scriptGuid]
        } else { '' }
        $goMatch = [regex]::Match($block, 'm_GameObject:\s*\{fileID:\s*(\-?\d+)\}')
        $goId = if ($goMatch.Success) { $goMatch.Groups[1].Value } else { '' }
        $goName = if ($goId -and $gameObjectNames.ContainsKey($goId)) { $gameObjectNames[$goId] } else { '' }
        $fields = foreach ($line in $document.Lines) {
            if ($line -match '^  ([A-Za-z_][A-Za-z0-9_]*):') {
                $field = $Matches[1]
                if ($standardMonoFields -notcontains $field) { $field }
            }
        }
        $fields = @($fields | Select-Object -Unique)
        $targetComponent = if ($targetMappings.ContainsKey($scriptType)) { $targetMappings[$scriptType] } elseif ($scriptPath -like 'Library/PackageCache/com.unity.render-pipelines.high-definition*') { 'HDRP compatibility staging; conversion decision pending' } else { 'Preserve GUID/type until explicit component mapping is approved' }
        $serializationRows.Add([pscustomobject]@{
            AssetPath=$asset.AssetPath
            ArtifactType=if ($asset.Extension -eq '.unity') {'Scene'} elseif ($asset.Extension -eq '.prefab') {'Prefab'} else {'ScriptableObjectOrAsset'}
            GameObject=$goName
            ScriptGuid=$scriptGuid
            OldType=$scriptType
            ScriptPath=$scriptPath
            SerializedFields=($fields -join ';')
            TargetComponent=$targetComponent
            PreservationRule='Preserve original .cs.meta when possible; use MovedFrom/FormerlySerializedAs or an exact-field compatibility shim; Missing Script prohibited'
            Resolution=if ($scriptPath) {'Resolved'} else {'Unresolved'}
        })
    }
}
$serializationRows | Sort-Object ArtifactType, AssetPath, GameObject, OldType | Export-Csv -LiteralPath (Join-Path $output 'SerializationMigrationTable.csv') -NoTypeInformation -Encoding UTF8

$sourceScripts = Get-ChildItem -LiteralPath (Join-Path $source 'Assets') -Recurse -File -Filter '*.cs'
$serializationAttributeEvidence = foreach ($script in $sourceScripts) {
    Select-String -LiteralPath $script.FullName -Pattern 'FormerlySerializedAs|MovedFrom' | ForEach-Object {
        [pscustomobject]@{
            Script=Get-RelativeForwardPath -Root $source -Path $script.FullName
            Line=$_.LineNumber
            Attribute=$_.Line.Trim()
        }
    }
}
$serializationAttributeEvidence | Export-Csv -LiteralPath (Join-Path $output 'SerializationAttributeEvidence.csv') -NoTypeInformation -Encoding UTF8

$persistencePatterns = 'PlayerPrefs|persistentDataPath|JsonUtility|Newtonsoft|File\.|Directory\.|SaveUtil|AES|Encrypt|Decrypt|BinaryFormatter'
$persistenceHits = foreach ($script in $sourceScripts) {
    Select-String -LiteralPath $script.FullName -Pattern $persistencePatterns | ForEach-Object {
        [pscustomobject]@{
            Script=Get-RelativeForwardPath -Root $source -Path $script.FullName
            Line=$_.LineNumber
            Evidence=$_.Line.Trim()
        }
    }
}
$persistenceHits | Export-Csv -LiteralPath (Join-Path $output 'PersistenceCodeEvidence.csv') -NoTypeInformation -Encoding UTF8

$persistenceSchema = @(
    [pscustomobject]@{ Store='PlayerPrefs'; Key='CompanyWar.LastLevel'; ValueType='String'; Status='Active'; Compatibility='Preserve key and semantics' }
    [pscustomobject]@{ Store='PlayerPrefs'; Key='CompanyWar.LevelStars.{LevelId}'; ValueType='Int'; Status='Active'; Compatibility='Preserve dynamic key format and star range' }
    [pscustomobject]@{ Store='PlayerPrefs'; Key='Audio settings keys (exact names in evidence report)'; ValueType='Float/Int'; Status='Active'; Compatibility='Preserve keys/defaults before AudioKit adaptation' }
    [pscustomobject]@{ Store='Encrypted file / SaveUtil'; Key='Callers not found in current source audit'; ValueType='AES encrypted payload'; Status='Dormant'; Compatibility='Do not remove until compatibility and historical-save decision' }
)
$persistenceSchema | Export-Csv -LiteralPath (Join-Path $output 'PersistenceSchema.csv') -NoTypeInformation -Encoding UTF8

$settingsPolicies = @{
    'ProjectVersion.txt'='Keep target; exact lock to 2022.3.62f3; never copy source file'
    'ProjectSettings.asset'='Field-level merge only: identity, resolution, color/input/API/backend/identifiers/stripping'
    'EditorBuildSettings.asset'='Rebuild from approved scene manifest; remove stale references only after approval'
    'GraphicsSettings.asset'='Keep target baseline pending HDRP/URP conversion decision; no whole-file copy'
    'QualitySettings.asset'='Field-level quality-tier and platform override comparison'
    'HDRPProjectSettings.asset'='Record source settings; do not copy blindly'
    'URPProjectSettings.asset'='Keep target baseline; final render path pending parity tests'
    'InputManager.asset'='Semantic merge of code-referenced axes/buttons'
    'TagManager.asset'='Merge tags/layers/sorting layers by name and fixed index'
    'TimeManager.asset'='Compare fixed/max timestep and timescale; keep target when equivalent'
    'DynamicsManager.asset'='Field-level 3D physics and collision-matrix migration'
    'Physics2DSettings.asset'='Field-level 2D physics and collision-matrix migration'
    'AudioManager.asset'='Manual merge of sample rate, DSP, speaker and disable settings'
    'NavMeshAreas.asset'='Migrate numeric areas/costs only when referenced by gameplay'
    'MemorySettings.asset'='Keep target unless platform measurements justify changes'
    'EditorSettings.asset'='Keep target; enforce text serialization/meta/line-ending requirements'
    'VersionControlSettings.asset'='Keep target Visible Meta Files workflow'
    'PackageManagerSettings.asset'='Keep target registries; dependencies handled by compatibility matrix'
    'ScriptableBuildPipeline.json'='Retain as Addressables compatibility candidate'
    'PresetManager.asset'='Migrate only referenced presets'
    'SceneTemplateSettings.json'='Keep target unless source templates are required'
    'ShaderGraphSettings.asset'='Follow approved render-pipeline decision'
    'VFXManager.asset'='Migrate only if VFX Graph dependency is proven'
    'XRSettings.asset'='Migrate only for approved XR target'
    'TimelineSettings.asset'='Migrate only if Timeline assets/calls are proven'
    'UnityConnectSettings.asset'='Never copy cloud IDs/services; configure intentionally'
}
$sourceSettings = Get-ChildItem -LiteralPath (Join-Path $source 'ProjectSettings') -File | Select-Object -ExpandProperty Name
$targetSettings = Get-ChildItem -LiteralPath (Join-Path $target 'ProjectSettings') -File | Select-Object -ExpandProperty Name
$allSettings = @($sourceSettings + $targetSettings + $settingsPolicies.Keys) | Sort-Object -Unique
$settingsRows = foreach ($name in $allSettings) {
    $sourcePath = Join-Path $source "ProjectSettings\$name"
    $targetPath = Join-Path $target "ProjectSettings\$name"
    [pscustomobject]@{
        Setting=$name
        SourcePresent=Test-Path -LiteralPath $sourcePath
        SourceSHA256=if (Test-Path -LiteralPath $sourcePath -PathType Leaf) {(Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash.ToLowerInvariant()} else {''}
        TargetPresent=Test-Path -LiteralPath $targetPath
        TargetSHA256=if (Test-Path -LiteralPath $targetPath -PathType Leaf) {(Get-FileHash -LiteralPath $targetPath -Algorithm SHA256).Hash.ToLowerInvariant()} else {''}
        Policy=if ($settingsPolicies.ContainsKey($name)) {$settingsPolicies[$name]} else {'Keep target unless a source dependency proves field-level migration is necessary'}
        ApprovalStatus='Pending'
        Verification='Diff fields; import/build/play tests; never whole-file copy unless separately approved'
    }
}
$settingsRows | Export-Csv -LiteralPath (Join-Path $output 'ProjectSettingsMigrationMatrix.csv') -NoTypeInformation -Encoding UTF8

$snapshotDir = Join-Path $target "Migration\Baseline\Cow\$SnapshotId"
$summary = @(
    '# Company War-RE first-batch migration inventory'
    ''
    "Generated: $([DateTime]::Now.ToString('yyyy-MM-dd HH:mm:ss zzz'))"
    ''
    '- Source project was read only during inventory generation.'
    '- Target editor is locked to Unity 2022.3.62f3.'
    '- No source scene, prefab, ScriptableObject, production script, or gameplay resource was imported.'
    '- Addressables, UniTask, DOTween, HDRP, StreamingAssets, and ResKit remain compatibility decisions, not removal commitments.'
    '- QFramework is restricted to Application, Infrastructure, and Presentation boundaries. Domain remains pure C#.'
    "- Recoverable snapshot verification: $(if (Test-Path -LiteralPath (Join-Path $snapshotDir 'restore-verification.txt')) {'present'} else {'missing'})"
    ''
    "Asset rows: $(@($assetRows).Count)"
    "GUID rows: $($guidRows.Count)"
    "GUID reference rows: $($uniqueReferenceRows.Count)"
    "Serialization rows: $($serializationRows.Count)"
    "Persistence evidence rows: $(@($persistenceHits).Count)"
)
[System.IO.File]::WriteAllLines((Join-Path $output 'README.md'), $summary, [System.Text.UTF8Encoding]::new($false))

Write-Output "First-batch inventory generated: $output"
