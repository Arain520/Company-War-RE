# Cow industrial environment migration v1

## Scope

Source project remains read-only. The target map environment migrates Cow's formal industrial
surround without importing Cow runtime, Domain code, HDRP materials, or ProjectSettings.

## Migrated source models

| Cow source | Target asset | SHA-256 |
|---|---|---|
| `_Game/Art/Models/Units/货运单元2.fbx` | `Content/Environment/Cow/Models/货运单元2.fbx` | `695A30F79DB0138245999C296F9B9C522B6BA76F780E2640C746C11B2179CA4F` |
| `_Game/Art/Models/Units/货运单元1.fbx` | `Content/Environment/Cow/Models/货运单元1.fbx` | `5A70E7D98EBAA5E2DA07B4FEB0755B02E1E8D8BA0994099A4C353D9CA3262D82` |
| `_Game/Art/Models/Units/栏杆左到右123.fbx` | `Content/Environment/Cow/Models/栏杆左到右123.fbx` | `FDC6D4744B47450DCB3D4F35035A914DDE778B0D51D1B5293E94CC407371ED7C` |
| `_Game/Art/Models/Units/服务器终端.fbx` | `Content/Environment/Cow/Models/服务器终端.fbx` | `52F7898B6C358B93701E90F2ECB9AFCC70EDD7BFA7821CFE92298EF516721DA0` |
| `_Game/Art/Models/Units/储装罐.fbx` | `Content/Environment/Cow/Models/储装罐.fbx` | `F1F76DA169D860AD0D11471DB7F37E7511CB493CDC0E50C887ABFA452E2E660B` |
| `_Game/Art/Models/Units/信号基站（拆件.fbx` | `Content/Environment/Cow/Models/信号基站（拆件.fbx` | `B4ECA897D49D64F28B18DDA098CC0E1C94959C47D702D32B9A5B1A5425D1AF74` |
| `_Game/Art/Models/Buildings/棋盘part (1).fbx` | `Content/Environment/Cow/Models/棋盘part (1).fbx` | `589EDD3577534157A414C1E19E6F91ECB6B7093EE89AF002294D75C08CD38958` |

Original `.meta` files are preserved. Target import did not detect GUID collisions.

## Procedural Cow elements

Cow did not contain dedicated pipe, stair, or skyline assets. These remain procedural and are
rebuilt by `CowIndustrialEnvironmentView` using URP-compatible primitives:

- six edge railing placements, preferring the migrated railing FBX;
- four pipe runs with supports;
- two stair groups with rails;
- deterministic perimeter skyline blocks and towers;
- six optional migrated environment model placements.

## Runtime and editor contract

- `FormalBattleMapEditorWindow` installs `CowIndustrialEnvironmentView` below `EnvironmentRoot`
  for every newly created map prefab.
- Existing map prefabs can use **安装/更新 Cow 工业环境**.
- The editor baker writes `BakedCowEnvironment` into each map prefab; formal battle never creates
  environment geometry at runtime.
- The default 18x30 map uses a square 240x240 environment footprint. Its side is derived from the
  longer board dimension at the approved 8x extent and exposes a sibling
  `EditableEnvironmentModelFill` for further manual
  placement. This manual root is preserved when the generated environment is rebaked.
- Generated objects that need individual adjustment must be converted with
  `GameObject > Company War-RE > 将所选环境对象转为手工对象`. Their generated name is
  recorded as an exclusion, so subsequent bakes do not recreate the original object.
- Imported Cow FBX renderers retain their original material-slot structure. Editor baking
  converts embedded colors and available base/normal/metallic/occlusion/emission textures to
  persistent URP/Lit materials. The uniform industrial palette is limited to procedural geometry.
- Environment objects use Ignore Raycast and have no enabled collider.
- Imported renderers preserve their material slots through URP conversion; procedural renderers
  use the target industrial palette. Cow HDRP materials are not used.
- Ground uses a repeating light-gray panel texture with dark seams and gold major boundaries.
- Skyline buildings are deterministically scattered across the square environment while a clear
  safety area is reserved around the battle board.
- Extent upgrades change ground dimensions, placement radius, and skyline density only; imported
  and procedural model scale values are not multiplied by the environment extent.
- The battle-board anchor remains independent and uniformly scaled.

## Acceptance

- All seven migrated models resolve as Unity GameObjects.
- Railings, pipes, stairs, skyline, and optional models are present after map preparation.
- No generated environment collider is enabled.
- Every generated environment renderer uses `Universal Render Pipeline/Lit`.
- Re-baking a map replaces the previous baked environment instead of stacking duplicates.
