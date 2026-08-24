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

## Implemented order and rules

1. E06/E07 are registered as stationary enemy combat actors from the legacy level
   configuration. They do not move, pollute territory, or emit zero-damage attacks.
2. A moving allied melee actor may approach and attack a building in the same small
   column. Building health and death use the existing simultaneous-damage pipeline.
3. Every enemy death awards its non-negative `AssaultScoreReward` exactly once.
   Score is retained for authorization/settlement display and does not directly win
   a building-objective battle.
4. An enemy-building death decrements the living building count and informs the
   wave scheduler. Spawn points for the affected control-block column are then
   recalculated before outcome evaluation.
5. Outcome priority matches current Cow runtime: no controlled territory is defeat;
   when building victory is disabled, no valid spawn point is defeat; when building
   victory is enabled, zero living enemy buildings is victory.
6. Reaching a terminal state stops future waves and time advancement. Deployment is
   rejected with `BattleEnded` until the test slice is reset.
7. Reset reconstructs territory, economy, combat actors, buildings, scheduler,
   score, and outcome state from immutable configuration.

The level compatibility adapter is optional. Older isolated tests that omit a level
remain open-ended; loading a legacy level enables full outcome evaluation.

## Test-scene composition

The target-owned slice uses a 9x9 compatibility level with one E06 at `(3,9)` and
one E07 at `(7,9)`. These are deliberately a small, traceable composition rather
than a wholesale copy of Cow's level directory.

## Deferred

- building occupation of all nine cells and cross-small-column melee targeting;
- E07/E12-E15 special production, attack, and aura behavior;
- death-animation delay before score/removal;
- authorization-choice UI and final settlement rewards;
- persistence of battle state, scheduler RNG, score, and living actors;
- production models, animation, VFX, SFX, pooling, and asset-loading strategy.
