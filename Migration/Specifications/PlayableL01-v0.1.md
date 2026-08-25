# Playable L01 presentation v0.1

Status: target-owned prototype presentation implemented

## Scope

This phase turns the already imported Cow L01 configuration into a readable,
interactive target scene without importing Cow scenes, prefabs, models, UI, or
other production assets. Domain rules and the compatibility adapter remain the
authority for battle state; Presentation only renders snapshots and sends existing
Application commands.

## Implemented presentation contract

- The `6x10` L01 macro grid is shown as an `18x30` small-cell battlefield.
- Both rows and columns receive a wider separator after every three cells, so each
  nine-cell control block is visually identifiable. Alternating block tint adds a
  second cue, and building footprints use a deep-red blocked-cell color.
- An angled orthographic camera calculates its home framing from the expanded grid.
  `WASD`/arrow keys and middle-mouse drag pan, the mouse wheel zooms, and `F` or
  `Home` restores the full-map view.
- Units and buildings use separate procedural silhouettes. Every living combatant
  has a template/HP label and a proportional health bar; buildings span the visual
  center of their complete 3x3 footprint.
- E13, E14, and E15 have stable prototype color/label cues. Snapshot HP changes and
  combat events produce curse, heal, execution, pollution, and death feedback.
  E15 also displays the remaining initial/cyclic execution time derived from its
  Application snapshot attack progress.
- The Chinese HUD reports economy, selection, time, wave, stage, alive counts,
  building count, score, spawn-point count, and battle state. Victory or defeat
  opens a central result panel with a reset action.

## Architecture boundary

`BattleSliceController`, camera, cell, combatant, and feedback views are
Presentation components. They consume `BattleSliceSnapshot` and send QFramework
commands through the existing Application boundary. No Unity or QFramework type is
introduced into Domain.

## Verification

- `BattleSliceSceneTests.L01Presentation_SeparatesControlBlockGroupsAndFitsAdaptiveCamera`
- `BattleSliceSceneTests.PlayablePresentationScripts_AreUnityAssetsWithoutGuidCollisions`
- compiled Domain, Infrastructure, and Application suites remain green
- solution compilation includes the new Presentation components and reports no
  compiler warning or error

An Editor Play Mode visual smoke test is still required after Unity imports the two
new MonoScripts. It must confirm full-map framing, pointer selection, camera input,
labels facing the camera, transient feedback, and result-panel reset.

## Deferred

- migrated or replacement production models, animation, UI Toolkit/uGUI layout,
  VFX, SFX, localization, pooling, and accessibility polish;
- composing E12-E15 into L01 itself (L01 currently declares E06/E07); their
  prototype Presentation paths are available for later fixtures/levels;
- final asset-loading and update strategy;
- persistence and final settlement UI.
