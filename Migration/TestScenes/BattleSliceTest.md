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
to three compatibility JSON TextAssets. At runtime the Presentation layer creates
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

- The left 54 green cells are owned; the right 27 gray cells reject deployment.
- U01 costs 1 resource and has a three-second per-unit cooldown, matching Cow's
  `Units.json`.
- Resources start at 10, gain one every five seconds, and receive one transmitter
  resource every three seconds.
- E01 starts as a red combatant at column 3, row 9.
- A successful deployment displays a cyan U01. Same-column U01/E01 actors advance,
  meet, exchange damage, and disappear on death; the HUD counts combat events.
- Pollution colors the whole selected 3 x 3 control block purple and blocks
  deployment there.
- The on-screen panel displays resources, elapsed time, cooldown, selection, and
  the latest success or rejection reason.

## Non-goals

This scene does not validate Cow art, final UI, full control-block combat, ranged or
special units, production spawning, save files, Addressables, ResKit,
StreamingAssets, or build configuration. It is not added to EditorBuildSettings and
does not alter ProjectSettings.
