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
the camera, light, 18 x 30 L01 grid, and placeholder combatants with health bars. These
placeholders are test visualization, not migrated production art.

## Run

1. Open `BattleSliceTest.unity` in Unity 2022.3.62f3.
2. Enter Play Mode.
3. Left-click a cell to select it.
4. Press `D` or `Space` to deploy U01. Right-click is reserved for camera orbit.
5. Hold the right mouse button and drag to rotate around the current focus. Use
   `WASD`/arrow keys or middle-mouse drag to pan, hold `Shift` for faster keyboard
   movement, use the wheel to zoom toward the pointer, and press `F` or `Home` to
   restore the full-map camera.
6. Press `P` to toggle pollution for the selected 3 x 3 control block.
7. Press `R` to reset the slice.

## Expected behavior

- Cow L01 expands from a `6 x 10` macro grid into `18 x 30` small cells. Ownership,
  starting resources, income, cooldown, and waves come from the imported L01/runtime
  compatibility documents rather than the retained 15 x 9 characterization fixture.
- U01 costs 1 resource and has a three-second per-unit cooldown, matching Cow's
  `Units.json`.
- Resources start at 10, gain one every five seconds, and receive one transmitter
  resource every three seconds.
- Six source buildings map, in source order, to centers `(2,29)`, `(5,29)`,
  `(14,29)`, `(17,29)`, `(8,29)`, and `(11,23)`. Each occupies all nine cells of
  its 3x3 control block.
- Wider gaps after every third row and column, alternating block tint, and deep-red
  occupied cells make each nine-cell control block and building footprint distinct.
- The angled orthographic camera initially fits the full battlefield. Cow-style
  right-drag orbit clamps pitch to 25-75 degrees; movement and zoom remain inside
  battlefield bounds, ignore pointer input over UI, and transition smoothly.
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
- Deploy U01 in any of the three columns covered by a building to advance toward
  and damage that same building. Enemy deaths add assault score exactly once.
- Destroying all six buildings changes the HUD state to `Victory` and stops waves.
  Losing every controlled block changes it to `Defeat`. Press `R` to reconstruct
  the complete initial battle state.
- Units and buildings display template IDs, HP values, and proportional health bars.
  E13-E15 fixtures receive prototype ability colors, labels, and transient feedback;
  L01 itself currently declares E06/E07 only.
- The Chinese on-screen panel displays resources, elapsed time, cooldown, selection,
  stage, wave, score, alive/building counts, valid spawn columns, and battle state.
  A terminal-state panel reports victory/defeat and provides a reset button.

## Non-goals

This scene does not validate Cow art, final UI, ranged or special units, save files,
Addressables, ResKit,
StreamingAssets, or build configuration. It is not added to EditorBuildSettings and
does not alter ProjectSettings.
