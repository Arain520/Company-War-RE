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
to two compatibility JSON TextAssets. At runtime the Presentation layer creates the
camera, light, 6 x 6 grid, and placeholder unit markers. These placeholders are test
visualization, not migrated production art.

## Run

1. Open `BattleSliceTest.unity` in Unity 2022.3.62f3.
2. Enter Play Mode.
3. Left-click a cell to select it.
4. Right-click, press `D`, or press `Space` to deploy U01.
5. Press `P` to toggle pollution for the selected 3 x 3 control block.
6. Press `R` to reset the slice.

## Expected behavior

- The left 18 green cells are owned; the right 18 gray cells reject deployment.
- U01 costs 1 resource and has a three-second per-unit cooldown, matching Cow's
  `Units.json`.
- Resources start at 10, gain one every five seconds, and receive one transmitter
  resource every three seconds.
- A successful deployment displays a cyan unit marker.
- Pollution colors the whole selected 3 x 3 control block purple and blocks
  deployment there.
- The on-screen panel displays resources, elapsed time, cooldown, selection, and
  the latest success or rejection reason.

## Non-goals

This scene does not validate Cow art, final UI, combat, spawning, save files,
Addressables, ResKit, StreamingAssets, or build configuration. It is not added to
EditorBuildSettings and does not alter ProjectSettings.
