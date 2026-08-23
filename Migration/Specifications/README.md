# Behavior specification and Domain rewrite skeleton

This directory is the evidence-backed contract for the second migration batch.
It does not authorize scene, prefab, ScriptableObject, texture, audio, or other
game-resource migration.

- `DomainBehavior-v0.1.md` records locked, provisional, conflicting, and deferred behavior.
- `CombatBehavior-v0.1.md` records the initial U01-versus-E01 combat boundary.
- `CombatBehavior-v0.2.md` is the current 3×3 melee encounter contract.
- `EnemyWaves-v0.1.md` is the current staged enemy generation contract.
- `LegacyBehaviorTraceability.csv` maps each behavior to Cow evidence and target tests.
- `SourceEvidenceHashes.csv` pins the audited Cow source files without modifying Cow.

The target implementation lives in `Assets/CompanyWarRE/Domain`. It is a pure C#
assembly with `noEngineReferences: true`. Unity, QFramework, Addressables, UniTask,
DOTween, ResKit, file I/O, and presentation concerns remain outside Domain.
