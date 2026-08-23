# Company War-RE migration workspace

This directory contains read-only audit outputs, recoverable Cow snapshots, and migration tooling.

First-batch constraints:

- `D:\UNITY2\Cow` is a read-only source.
- Unity scenes, prefabs, ScriptableObjects, production scripts, and gameplay resources are not imported in this batch.
- The target editor version is locked to Unity 2022.3.62f3.
- Dependency changes and render-pipeline conversion require a later approval gate.
- QFramework stays outside the pure C# Domain layer.

Run `Tools/Create-CowSnapshot.ps1` before `Tools/Generate-FirstBatchInventory.ps1`. Both scripts only write below the target project.
