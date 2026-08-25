# Formal battle startup v1

Status: L02-L05 are wired to a target-owned production scene boundary.

## Startup scene

`Assets/CompanyWarRE/Scenes/FormalBattle.unity` is enabled at build index 0. It
contains only target-owned bootstrap components and serialized references to:

- the complete U01-U36 and E01-E15 catalogs;
- the shared Cow-compatible spawn schedules;
- the four independently migrated formal documents L02-L05.

The serialized `formalLevelId` selects the startup document and defaults to
`L02`. Change this field to `L03`, `L04`, or `L05` to launch another imported
level. An ID/document mismatch is a startup error rather than a silent fallback.

`BattleSliceTest.unity` remains on the legacy L01 compatibility path and is not
modified into a production scene.

## Runtime flow

1. Presentation resolves the selected TextAsset by formal level ID.
2. Infrastructure parses and validates the formal document plus the shared
   catalogs and schedules.
3. The resulting `BattleSliceConfiguration` crosses the QFramework boundary
   through `ConfigureBattleSliceCommand`.
4. Application resets the pure C# Domain model, constructing the runtime grid,
   six initial controlled rows, enemy buildings, selected wave stages, spawn
   points, resources, and the building-victory objective.
5. Presentation consumes `FormalLevelRuntimeMetadata.Environment` through the
   separate `BattleSliceEnvironmentView` component.

Configuration errors are displayed in the runtime HUD and written to the Unity
log with structured code/path/message details. Formal startup never falls back
to L01 when its selected document is absent or invalid.

## Environment boundary

The current environment consumer builds a deterministic industrial placeholder
from `EnvironmentId`, `DecorRing`, outer-ground size, skyline distance/density,
seed, and optional scene-prop placements. Auto values follow the recorded Cow
rules: outer ground is at least 80 units and skyline distance is 44% of it.

This component is a replaceable Presentation boundary. It does not select
StreamingAssets, ResKit, or Addressables as the final art delivery system, and
configured prop markers can later be replaced by the approved prefab registry
without changing Domain or the level schema.

## Verification

- Infrastructure tests execute L02-L05 through the formal pipeline.
- Migration scene tests verify all four documents, complete catalogs, scripts,
  default level ID, environment consumer, and missing-component count.
- The full solution must compile before committing this scene manifest.
