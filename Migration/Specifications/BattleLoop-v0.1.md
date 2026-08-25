# Battle loop v0.1 — enemy buildings, score, and outcome

Status: executable Domain/Application vertical slice

## Cow evidence

- `BattleEngine.CleanupPhase`, `AddAssaultScore`, and `CheckVictoryOrDefeat`
- `BattleEngine.IsBuilding`
- `SpawnEngine.DestroyBuildingAt`
- `ConfigModels.LevelConfigDef` and `BuildingPlacement`
- `Enemies.json` entries E06 and E07
- `Levels/L01.json`
- `CW_Core_08_VictoryByPointsTests`

All audited Cow files are read-only and pinned in `SourceEvidenceHashes.csv`.

## Confirmed building footprint

Cow's current `GridMap.TryOccupyControlBlockWithBuilding` marks every cell in a
3x3 control block. The target follows that behavior: a building occupies all nine
cells of the control block containing its configured position. Columns are grouped
in sets of three, and the executable test scene adds a wider visual gap between
adjacent column groups.

## Implemented order and rules

1. E06/E07 are registered as stationary enemy combat actors from the legacy level
   configuration. They do not move, pollute territory, or emit zero-damage attacks.
2. Each building blocks and occupies all nine cells in one 3x3 control block. The
   complete block must fit inside the grid and cannot overlap another building block.
3. A moving allied melee actor may approach and attack a building from any of its
   three footprint columns. Multiple columns may focus the same building. Building
   health and death use the existing simultaneous-damage pipeline.
4. Every enemy death awards its non-negative `AssaultScoreReward` exactly once.
   Score is retained for authorization/settlement display and does not directly win
   a building-objective battle.
5. An enemy-building death clears all nine occupied cells, decrements the living
   building count, and informs the wave scheduler. Spawn points for the affected
   control-block column are then
   recalculated before outcome evaluation.
6. Outcome priority matches current Cow runtime: no controlled territory is defeat;
   when building victory is disabled, no valid spawn point is defeat; when building
   victory is enabled, zero living enemy buildings is victory.
7. Reaching a terminal state stops future waves and time advancement. Deployment is
   rejected with `BattleEnded` until the test slice is reset.
8. Reset reconstructs territory, economy, combat actors, buildings, scheduler,
   score, and outcome state from immutable configuration.

The level compatibility adapter is optional. Older isolated tests that omit a level
remain open-ended; loading a legacy level enables full outcome evaluation.

## Test-scene composition

The target-owned slice is widened from 9x9 to 15x9. It places building centers at
`(2,9)`, `(8,9)`, and `(14,9)`, producing non-overlapping 3x3 footprints at columns
`1-3`, `7-9`, and `13-15`, all across rows `7-9`. This remains a small, traceable
composition rather than a wholesale copy of Cow's level directory.

## Deferred

- E12 executable-scene coverage and E13-E15 special behavior; the Cow audit found
  no E06/E07 ID-specific production, spawning, or aura branch (see
  `EnemyBuildings-v0.1.md`);
- death-animation delay before score/removal;
- authorization-choice UI and final settlement rewards;
- persistence of battle state, scheduler RNG, score, and living actors;
- production models, animation, VFX, SFX, pooling, and asset-loading strategy.
