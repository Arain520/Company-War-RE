# Enemy building behavior v0.1

Status: E06/E07/E12 baseline and E13 curse implemented; E14-E15 special slice deferred

## Evidence

- `Assets/_Game/Configs/Enemies.json`
- `Assets/_Game/Scripts/Logic/Core/Domain/BattleEngine.cs`
- `Assets/_Game/Scripts/Logic/Core/Domain/GridMap.cs`
- `Assets/_Game/Scripts/Logic/Core/Domain/SpawnEngine.cs`

All evidence was read from Cow without modifying it. Exact source hashes are pinned
in `SourceEvidenceHashes.csv`.

## Audited behavior matrix

| ID | Cow configuration | Runtime behavior | Target status |
| --- | --- | --- | --- |
| E06 污秽之地 | HP 7, attack 0, speed 0, raw interval 0, raw range 0, reward 1 | Static nine-cell building. No ID-specific production, attack, aura, or spawn branch was found. | Implemented through generic stationary-building rules. |
| E07 污秽巢穴 | HP 12, attack 0, speed 0, raw interval 0, raw range 0, reward 2 | Static nine-cell building. Despite its name, no ID-specific production or spawning branch was found. | Implemented through generic stationary-building rules. |
| E12 虚伪皮层 | HP 15, attack 0, speed 0, raw interval 0, raw range 0, reward 2 | Static nine-cell building with no ID-specific branch. | Configuration and Domain characterization implemented; executable scene coverage is deferred. |
| E13 恶魔 | HP 18, attack 1, speed 0, interval 0.5 s, range 3 blocks, reward 5 | Performs normal attacks and applies a persistent curse before movement and attack phases. | Persistent curse implemented in Domain and verified through Application; dedicated VFX is deferred. |
| E14 巨兽 | HP 20, attack 1, speed 0, interval 1 s, range 1 block, reward 5 | Performs normal attacks and heals 1 HP for every target it kills. | Deferred. |
| E15 咒灭术师 | HP 15, attack 0, speed 0, interval 12 s, range 99 blocks, reward 5 | Uses an ID-specific execution cast rather than the zero-attack generic path. | Deferred. |

## E13 persistent curse contract

- Every living enemy E13 is a source.
- Every living ally is eligible, including allied buildings; Cow does not exclude
  buildings in this loop.
- Source-to-target range is Manhattan distance in control blocks and uses the
  source's configured range, with a minimum of one.
- Each affecting source contributes `0.1 HP/second`; multiple E13 sources stack.
- Fractional damage accumulates on the target's shared damage carry. Only completed
  integer damage is removed from HP, and the remainder is retained.
- The effect runs before movement, normal attacks, cleanup, and outcome evaluation.

## E14 kill-heal contract

- E14 follows the normal target-selection and pending-damage pipeline.
- For every living target changed to dead by E14's applied damage, E14 gains 1 HP.
- Cow does not clamp this healing to initial durability.
- E14 is not in the heavy-strike set, so one ordinary attack has one primary target.

## E15 execution contract

- Attack progress accumulates continuously. Every completed 12-second interval
  produces one cast; a large tick may produce multiple casts and retains remainder.
- Eligible targets are living opponents that are not buildings. When E15 is an
  enemy, U30/U31 stealth actors are also excluded.
- Range uses Manhattan control-block distance.
- Selection order is lowest HP, then shortest control-block distance, then shortest
  small-cell Manhattan distance. Stable actor order resolves any remaining tie.
- The cast records an attack event for the target's current HP and immediately sets
  that target to zero HP.

## Cross-cutting building rules

- All six IDs are classified as buildings and are stationary.
- Their configured cell resolves to the center of its control block in Cow; the
  complete control block is occupied and blocked by the building.
- Enemy buildings do not pollute territory and are excluded from end-line
  self-destruction.
- Death clears the occupied block, updates enemy-building count and spawn points,
  awards score once, and then participates in outcome evaluation.

## Implemented tests

- `CombatSimulationBehaviorTests.E06E07AndE12_AreStationaryDataOnlyBuildings`
- `CombatSimulationBehaviorTests.E13Curse_AccumulatesFractionalDamageAndStacksLivingSources`
- `CombatSimulationBehaviorTests.E13Curse_UsesControlBlockRangeAndIncludesAlliedBuildings`
- `CombatSimulationBehaviorTests.E13Curse_ResolvesBeforeMovementAndAttackPhases`
- `BattleSliceApplicationTests.E13Curse_FlowsThroughApplicationSnapshotBeforeUnitAction`

## Next implementation order

1. Add E14 kill-heal tests, including over-healing beyond initial durability.
2. Add E15 execution tests for target exclusions, deterministic priority, large
   ticks, and interval remainder.
3. Add Presentation feedback for persistent curse, healing, and execution only
   after the Domain contracts are stable.

## Risks and open evidence gaps

- Cow has no dedicated test case for E13-E15; these contracts are observed directly
  from the pinned `BattleEngine.cs`. Target characterization tests now lock E13;
  E14/E15 still require equivalent tests before scene integration.
- Special behavior is selected by hard-coded template IDs rather than a data-driven
  ability field. The compatibility layer must preserve IDs exactly.
- Cow clamps raw zero range/interval when creating runtime actors. E06/E07/E12 stay
  inert because attack is zero; target configuration DTOs should still preserve the
  raw JSON values separately from runtime-safe values.
- `DamageCarry` is shared by fractional damage sources. Refactoring the curse into a
  separate accumulator would change interactions and requires explicit approval.
