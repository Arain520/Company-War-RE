# BattleSliceTest

Scene: `Assets/CompanyWarRE/Scenes/BattleSliceTest.unity`

## Purpose

This target-owned scene validates the first complete migration path without
importing Cow scenes or resources:

```
Unity input and runtime visuals
    -> QFramework commands and queries
    -> pure C# Domain rules
    -> presentation snapshot
```

The scene intentionally contains only `BattleSliceBootstrap` plus direct references
to five compatibility JSON TextAssets. At runtime the Presentation layer creates
the camera, light, 9 x 9 grid, and placeholder combatants with health bars. These
placeholders are test visualization, not migrated production art.

## Run

1. Open `BattleSliceTest.unity` in Unity 2022.3.62f3.
2. Enter Play Mode.
3. Left-click a cell to select it.
4. Right-click, press `D`, or press `Space` to deploy U01.
5. Press `P` to toggle pollution for the selected 3 x 3 control block.
6. Press `R` to reset the slice.

## Expected behavior

- The lower rows 1-6 (54 green cells) are owned; upper rows 7-9 (27 gray cells)
  form the enemy region and reject deployment.
- U01 costs 1 resource and has a three-second per-unit cooldown, matching Cow's
  `Units.json`.
- Resources start at 10, gain one every five seconds, and receive one transmitter
  resource every three seconds.
- E06 and E07 start as enlarged magenta enemy buildings at `(3,9)` and `(7,9)`.
- Staged waves begin after ten seconds. Enemies are generated from valid columns;
  buildings move their control-block columns' spawn points forward.
- A successful deployment displays a cyan U01. Same-column U01/E01 actors advance,
  meet, exchange damage, and disappear on death; the HUD counts combat events.
- U01 and E01 in different small columns but the same 3×3 control-block column now
  bind to a shared battle block, move to reserved faction rows, and can fight inside
  that block. Multiple deployed U01 actors may focus one E01.
- If E01 reaches owned row 6 without a living ally melee defender in that 3×3
  control block, rows 4-6 / columns 1-3 turn purple, lose ownership, and E01 dies.
- Pollution colors the whole selected 3 x 3 control block purple and blocks
  deployment there.
- Deploy U01 in columns 3 or 7 to advance toward and damage the corresponding
  building. Enemy deaths add assault score exactly once.
- Destroying both buildings changes the HUD state to `Victory` and stops waves.
  Losing every controlled block changes it to `Defeat`. Press `R` to reconstruct
  the complete initial battle state.
- The on-screen panel displays resources, elapsed time, cooldown, selection, and
  the latest success or rejection reason, plus stage, wave, score, building count,
  valid spawn columns, and battle state.

## Non-goals

This scene does not validate Cow art, final UI, ranged or special units, save files,
Addressables, ResKit,
StreamingAssets, or build configuration. It is not added to EditorBuildSettings and
does not alter ProjectSettings.
