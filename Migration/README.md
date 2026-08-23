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
