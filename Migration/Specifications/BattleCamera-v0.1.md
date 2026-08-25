# Battle camera v0.1

Status: Cow-style target adaptation implemented

## Cow evidence

- `Assets/_Game/Scripts/Prototype3D/BattleCameraController.cs`
- `Assets/_Game/Scripts/Prototype3D/CompanyWarPrototype3DRuntime.cs`
  (`EnsureFreeCameraController` and `CompanyWarFreeCameraController`)

Cow remains read-only. The exact source hashes are pinned in
`SourceEvidenceHashes.csv`.

## Observed Cow controls

- The runtime disables its free-camera component and enables
  `BattleCameraController` around a `MapCenterPivot` at the battlefield center.
- Holding the right mouse button rotates yaw and pitch; pitch is clamped to
  25-75 degrees and rotation is ignored while the pointer is over UI.
- The wheel changes the clamped orbit distance.
- Position and look rotation use a 0.12-second smooth transition with unscaled time.
- Cow's retained fixed-angle free-camera path adds camera-relative WASD movement,
  Shift acceleration, zoom toward the mouse-plane hit, and bounded focus movement.

## Target adaptation

The target keeps its orthographic full-L01 composition, so orbit-distance zoom is
mapped to orthographic-size zoom. The resulting controls are:

- right-drag: orbit around the current battlefield focus;
- wheel: bounded zoom, focusing toward the pointer when zooming in;
- WASD/arrow keys: camera-relative pan; Shift accelerates it;
- middle-drag: pan;
- `F`/`Home`: restore full-map focus, pitch, yaw, and zoom;
- focus is clamped to the `18x30` battlefield extents;
- focus, yaw, pitch, and zoom use Cow's 0.12-second unscaled-time smoothing;
- rotation and wheel input are ignored while the pointer is over UI.

Right-click deployment was removed because it conflicts with Cow-style orbit.
Deployment remains available through `D` and `Space` after left-click selection.

## Architecture

Camera input and interpolation remain entirely in Presentation. They do not change
Application commands, Domain state, legacy configuration, scenes, or ProjectSettings.

## Verification

- `BattleSliceSceneTests.L01Presentation_SeparatesControlBlockGroupsAndFitsAdaptiveCamera`
- `BattleSliceSceneTests.CowStyleCameraControl_ClampsZoomPitchAndBattlefieldFocus`
- Unity Play Mode visual check: UI exclusion, right-drag orbit, pitch limits,
  mouse-directed zoom, bounded pan, smoothing, reset, selection, and deployment
