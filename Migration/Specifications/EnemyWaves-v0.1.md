# Enemy waves v0.1

Status: executable Domain/Application vertical slice

## Cow evidence

- `SpawnEngine.SetupStages`, `Tick`, `SpawnWave`, and `PickWeighted`
- `SpawnEngine.SetupLevel`, `RegisterBuildingAt`, `DestroyBuildingAt`, and
  `HasAnyValidSpawnPoint`
- `SpawnSchedules.json`
- `CW_UNIT_0104_SpawnEngine`
- `CW_Core_03_SpawnPointRetreatTests`
- `CW_Core_06_StageScheduleTimingTests`
- `CW_Core_07_SpawnPointColumnLossTests`

The audited files are pinned in `SourceEvidenceHashes.csv`; Cow remains read-only.

## Implemented behavior

- A stage preserves the legacy `Name`, `Period`, `Rate`, `PerWave`, and weighted
  `Types` schema. `Period` is converted from inclusive `mm:ss-mm:ss` bounds and
  the denominator of `Rate` is the interval in seconds.
- A wave is emitted at each completed interval. `PerWave` limits the number of
  enemies; if it is absent or non-positive, the `Rate` numerator is the fallback.
- Spawn point order and weighted enemy selection use a seeded pseudo-random source.
  The test slice fixes the seed to 17, so replays and tests are deterministic.
- A wave uses each eligible small-cell column at most once. When fewer columns are
  eligible than `PerWave`, the wave size shrinks.
- A column is eligible only while it contains at least one owned, unpolluted cell.
  Losing all controlled territory in a column stops future spawns in that column.
- Without an enemy building, the spawn point is the configured enemy edge row.
  An enemy building moves all spawn points in its 3-column control-block column to
  the row immediately before the building's control block. Destroying the last
  building in that block column retreats those points to the edge row.
- The Domain emits immutable spawn requests. The QFramework Application boundary
  resolves enemy definitions, assigns deterministic unique actor IDs, and registers
  actors with `CombatSimulation`. Presentation only renders snapshots and wave HUD.
- The test scene reads a serialized `TextAsset`; Domain and Application do not bind
  the configuration to StreamingAssets, ResKit, or any single update technology.

## Evidence conflict retained for review

The current Cow runtime normalizes buildings to a 3x3 control block and places the
spawn row before that block. Some older Cow test assertions describe the immediate
small cell before a building instead. This implementation follows current runtime
code and records the conflict rather than silently mixing both coordinate models.

## Deferred

- registering and destroying real enemy-building actors through Application;
- endless-loop stages and endless damage scaling;
- assault-score rewards and final victory/defeat orchestration;
- save/load of scheduler time, stage, RNG, wave index, and spawned actors;
- final spawn VFX, animation, audio, pooling, and production asset loading.
