# Combat behavior v0.1 — U01 versus E01

Status: executable minimal vertical slice

## Evidence boundary

This contract is characterized from Cow's `BattleEngine.cs`, `Enemies.json`, and
the three combat test files pinned in `SourceEvidenceHashes.csv`. Cow remains
read-only. The target does not copy the legacy engine; it rewrites the approved
behavior in pure C#.

## Locked values

| Unit | Team | Durability | Attack | Speed | Interval | Range | Reward |
|---|---|---:|---:|---:|---:|---:|---:|
| U01 | Ally | 1 | 1 | 1 | 1 s | 1 | n/a |
| E01 | Enemy | 1 | 1 | 1 | 1 s | 1 | 1 |

Both are moving `Staff` melee actors. Allies advance toward increasing row/lane
values; enemies advance toward decreasing values.

## Implemented behavior

- Only a living opponent in the same column and in front can be targeted.
- The battlefield advances along rows: allies move from low to high row numbers;
  enemies spawn in high, non-owned rows and move toward low, ally-owned rows.
- Moving melee actors close to legacy contact distance `0.12`, with epsilon `0.02`.
- A tick moves actors before resolving attacks.
- Attack progress advances only while a valid target exists and resets otherwise.
- Every completed attack interval produces one attack; a large tick may produce
  multiple attacks.
- Damage is accumulated and then applied, so two actors alive at attack collection
  time can kill each other simultaneously.
- Death is reported once. Dead actors neither move nor attack on later ticks.
- A surviving melee actor resumes forward movement when no opponent remains.
- When an enemy first occupies an owned cell and no living ally melee actor defends
  that 3×3 control block, the whole block becomes polluted and unowned. The invading
  enemy self-destructs and emits pollution followed by death events.
- A living ally melee actor anywhere in the invaded control block delays pollution,
  matching Cow's encounter-first rule.
- The Application layer removes a dead deployed ally from its original deployment
  cell and exposes combat actors/events through a read-only snapshot.

## Deliberate slice boundary

Cow's complete melee implementation reserves battle rows inside 3×3 control blocks
and supports multiple attackers, adjacent-block engagement, and more complex 2D
pursuit. Version 0.1 uses continuous lane positions and same-column engagement to
make U01-versus-E01 deterministic and reviewable. This simplification is not an
approval to replace the complete Cow behavior.

Deferred items include:

- reserved battle-row rotation and multi-actor focus rules;
- ranged targeting and projectiles;
- assault score integration after an enemy death or breach;
- buildings, support units, healing, conversion, execution, buffs, and every
  unit-specific effect;
- final art, animation, audio, death effect, spawning waves, victory/defeat wiring,
  save data, and production resource loading.

Any later expansion must add evidence, traceability rows, and characterization tests
before changing this contract.
