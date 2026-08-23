# Combat behavior v0.2 — 3×3 melee encounter

Status: executable control-block combat slice

This version extends `CombatBehavior-v0.1.md`. Values, vertical movement, pending
damage, death, and territory-breach rules remain unchanged unless stated here.

## Evidence

- `BattleEngine.RefreshMeleeEngagements`
- `BattleEngine.CreateMeleeEngagement`
- `BattleEngine.MoveActorTowardBattleLine`
- `BattleEngine.FindNearestMeleeOpponentInSameBlock`
- `BattleEngine.RefreshMeleeBattlefieldEvents`
- `CW_UNIT_0109_ControlBlockEncounter`

All evidence is read from Cow and pinned in `SourceEvidenceHashes.csv`.

## Implemented encounter rules

- A moving ally and enemy melee `Staff` may bind when they share a control-block
  column and occupy the same or adjacent control-block rows.
- For actors already in the same block, that block hosts the battle.
- For actors in adjacent blocks, arrival time at the boundary is calculated from
  distance and speed. The side arriving first selects its opponent's block. Equal
  arrival time is resolved deterministically by actor ID.
- The ally battle line is the block's first small row; the enemy battle line is its
  last small row. Actors move toward these reserved rows without horizontal chase.
- Once both teams have moving melee actors in the same 3×3 block, model distance
  and exact small-cell column no longer prevent melee attacks.
- Multiple melee attackers in the same block may select and damage one opponent in
  the same tick. Damage remains pending and resolves simultaneously.
- A battlefield-start event identifies the block and its center effect cell. A
  battlefield-end event is emitted after either team has no living melee actor in
  that block.
- A living ally melee actor in the invaded block prevents pollution. If combat
  eliminates the defender, a surviving enemy resumes movement on the next tick and
  may pollute when it later enters an undefended owned block.
- A surviving melee actor resumes normal forward movement after its engagement ends.

## Still deferred

- ranged cross-block targeting/projectiles and non-moving combatants beyond the
  already characterized same-column compatibility path;
- stealth and visibility rules;
- buildings, blocked control blocks, support effects, healing, conversion,
  execution, knockback, buffs, and unit-specific mechanics;
- final VFX/animation/audio for battlefield start, attack, death, and pollution;
- spawn waves, assault score, victory/defeat integration, and save data.
