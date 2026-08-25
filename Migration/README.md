# Company War-RE migration workspace

This directory contains read-only audit outputs, recoverable Cow snapshots, and migration tooling.

First-batch constraints:

- `D:\UNITY2\Cow` is a read-only source.
- Unity scenes, prefabs, ScriptableObjects, production scripts, and gameplay resources are not imported in this batch.
- The target editor version is locked to Unity 2022.3.62f3.
- Dependency changes and render-pipeline conversion require a later approval gate.
- QFramework stays outside the pure C# Domain layer.

Run `Tools/Create-CowSnapshot.ps1` before `Tools/Generate-FirstBatchInventory.ps1`. Both scripts only write below the target project.

Second-batch artifacts:

- `Specifications/DomainBehavior-v0.1.md` is the reviewed behavior contract draft.
- `Specifications/LegacyBehaviorTraceability.csv` connects Cow evidence to executable tests.
- `Assets/CompanyWarRE/Domain` contains the pure C# rewrite skeleton.
- `Assets/CompanyWarRE/Tests/Editor/Domain` contains EditMode characterization tests.

The second batch still imports no Cow scene, prefab, ScriptableObject, or production resource.

Test vertical slice:

- `Assets/CompanyWarRE/Scenes/BattleSliceTest.unity` connects runtime input and
  placeholder visuals to QFramework commands/queries and the pure C# Domain.
- `TestScenes/BattleSliceTest.md` records controls, expected behavior, and non-goals.
- The scene is intentionally excluded from build settings until the target platform
  and production scene flow are approved.

Configuration compatibility slice:

- `Specifications/ConfigCompatibility-v0.1.md` documents the DTO/validation/mapping boundary.
- `Specifications/LegacyConfigCompatibilityMatrix.csv` maps legacy fields to target values.
- The test scene receives JSON through serialized TextAsset references; this is a
  test composition choice, not a final commitment to StreamingAssets, ResKit, or Addressables.

Formal level startup:

- `Assets/CompanyWarRE/Scenes/FormalBattle.unity` is the target-owned build-index-0 scene.
- `Specifications/FormalBattleStartup-v1.md` records the L02-L05 selection and startup flow.
- Formal environment metadata is consumed through a replaceable Presentation component;
  final art delivery remains undecided.
