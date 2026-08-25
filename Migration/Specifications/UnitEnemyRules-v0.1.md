# Unit and enemy rules v0.1

Status: Cow behavior specification locked before target Presentation integration

## Configuration provenance

- U01-U24 come from Cow `Assets/StreamingAssets/CompanyWar/Configs/Units.json`.
- U25-U36 do **not** exist in either Cow `Units.json`; Cow appends them at runtime
  in `CompanyWarPrototype3DRuntime.EnsureEndlessUnitDefinitions`.
- E01-E15 come from Cow
  `Assets/StreamingAssets/CompanyWar/Configs/Enemies.json`.
- Target `LegacyUnits.All.json` and `LegacyEnemies.All.json` form one bounded,
  traceable compatibility catalog. They are not a wholesale Assets import.

## Unit matrix

| IDs | Cow role | Locked behavior |
| --- | --- | --- |
| U01-U05, U24-U25, U36 | moving staff | Move forward, stop for valid targets, and use configured HP/attack/speed/interval/range. |
| U06 | Strike support | Target the block one small row ahead; kill non-buildings and remove at most 5 HP from each enemy building in that block. |
| U07 | Bomb3x3 support | Affect a 3x3 area of control blocks; kill non-buildings and remove at most 5 HP from each building. |
| U08 | transmitter building | Occupy one complete 3x3 control block and register configured resource production until destroyed. |
| U09 | authorization building | Occupy one complete 3x3 control block and register configured authorization production until destroyed. |
| U10-U11 | barrier buildings | May deploy outside controlled territory, occupy a complete 3x3 block, and block movement/deployment. |
| U12 | freeze support | Freeze enemy movement for five simulation ticks. |
| U13-U14 | deployment echo | Deploy the primary actor, then attempt one/three extra copies on valid owned cells selected by Cow's runtime random ordering. |
| U15 | terrain build | Convert an adjacent uncontrolled control block to owned and clear its pollution when allowed. |
| U16, U22-U23 | heavy melee | A hit damages every living opponent in the primary target's control block. |
| U17-U18 | mobile ranged | Move until a forward target is within configured control-block range, then attack. |
| U19-U20 | heavy ranged | Use mobile ranged rules and damage every opponent in the target control block. |
| U21 | gratitude building | Every 2 seconds heal 1 HP on the lowest-HP living ally in the same control block; healing is not capped at initial durability. |
| U26/U34 | silence support | Multiply enemy speed by 0.5 for 10 seconds / 0.2 for 13 seconds; the stronger active multiplier wins and duration extends. |
| U27 | authority pushback | A zero-damage attack every 3 seconds pushes a non-building target three control blocks backward and sets a two-second negative action timer. |
| U28/U29 | lure support | Move affected non-building enemies to the target block column within radius 1/2 control blocks and apply a five-second negative action timer. |
| U30/U31 | stealth actors | Despite `Type=Building`, Cow treats them as non-buildings. Enemy targeting, melee binding, stopping, and E15 execution ignore them. |
| U32 | conversion building | Maximum three living copies. Every 30 seconds, select up to three enemy non-buildings nearest controlled territory, then source distance, and change them to Ally. |
| U33 | medical building | Every 2 seconds heal 1 HP on the lowest-HP ally within one control block on each axis; healing is not durability-capped. |
| U35 | Destroy5x5 support | Affect a radius-two area of control blocks using the same non-building kill/building 5-HP rule. |

## Enemy matrix

| IDs | Locked behavior |
| --- | --- |
| E01-E05, E08, E10 | Generic moving melee using configured values. |
| E09 | Moving heavy-strike melee; one hit affects every opponent in the target control block. |
| E11 | Generic moving melee; after completed death cleanup it spawns one E11 at its death cell. |
| E06/E07/E12 | Stationary nine-cell buildings without an additional ID-specific branch. |
| E13 | Stationary building, normal attack, plus stacking 0.1 HP/second persistent curse in Manhattan control-block range before movement/attack. |
| E14 | Stationary building; gains 1 HP for each target killed by its applied pending damage, without a durability cap. |
| E15 | Stationary execution building; initial cast after 15 seconds, then every 12 seconds, selecting lowest HP then block/cell distance and excluding buildings/stealth. |

## Shared targeting and timing

- All staff with positive speed move, including ranged staff.
- Attack range is measured forward in control-block rows within the same
  control-block column. Ranged movement stops once a target is in range.
- Enemy targeting cannot select U30/U31. Stealth actors also do not create melee
  engagements or stop an advancing enemy.
- Attacks are collected before damage is applied. Heavy-strike splash and E14 kill
  healing are resolved in pending-attack order.
- U21/U33 healing and U32 conversion consume completed intervals even when a large
  tick completes several actions.
- Negative `AttackProgress` is also the Cow movement/action lock used by U27 and lure.

## Architecture constraint

All catalog values and rules live in pure C# Domain or the Infrastructure
compatibility adapter. QFramework commands/models may orchestrate them, but Unity,
QFramework, prefab, UI, VFX, and audio types are prohibited in Domain.

## Deferred Presentation

No new ability Presentation is authorized by this specification. Models,
projectiles, healing/stealth/pushback/conversion feedback, selection UI, and final
unit deployment UI follow only after their Domain tests pass.
