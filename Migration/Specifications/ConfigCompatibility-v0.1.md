# Configuration compatibility layer v0.1

Status: first executable compatibility slice

## Scope

This layer reads the legacy `Units.json` and `Enemies.json` document shapes,
validates them, and maps one selected ally and enemy into the pure C# Domain model.
The target-owned samples preserve Cow U01 and E01 values. U01 retains resource cost
1 and deploy cooldown 3 seconds; both actors retain their combat statistics.

The test runtime settings preserve separately audited code defaults where relevant:
initial resources 10, fixed production every 5 seconds, and transmitter production
every 3 seconds. They are not presented as fields from Cow `Units.json`.

## Boundary

```
IConfigurationTextSource
    -> Legacy JSON DTOs
    -> validation with stable issue codes
    -> BattleSliceConfiguration + UnitDefinition + CombatantDefinition
    -> QFramework Application commands
```

`IConfigurationTextSource` accepts text by logical key. It has no file path,
StreamingAssets, Resources, ResKit, or Addressables dependency. The test scene uses
serialized TextAssets only as its composition adapter; a later production adapter
can replace this without changing DTO validation or Domain mapping.

## Compatibility rules

- JSON field names remain case-sensitive through explicit .NET data-contract names.
- The parser uses standard .NET serialization and has no Unity/QFramework dependency.
- Unit IDs are validated as non-empty and unique case-insensitively.
- Enemy IDs are validated as non-empty and unique case-insensitively; E01 combat
  ranges are validated before mapping.
- Resource cost and deploy cooldown cannot be negative.
- Legacy `Staff`, `Building`, and `Support` types map to explicit Domain deployment
  modes. `Support` with `TerrainBuild` maps to `TerrainBuild`; other support effects
  map to `SupportEffect`.
- Missing footprint values retain the legacy default `SmallCell` behavior.
- Unknown JSON fields are tolerated for forward compatibility.
- Unsupported schema versions, enum values, references, and ranges return stable
  issues instead of constructing a partial Domain configuration.

## Current sample versus full migration

`LegacyUnits.U01.json` and `LegacyEnemies.E01.json` are traceable one-record
compatibility samples, not complete migrated configuration files. U02-U36, E02-E15,
authorization, levels, and spawn schedules remain later batches.

`BattleSliceRuntime.json` is target-owned test composition. It is versioned with
`SchemaVersion: 1` and may be replaced when production configuration ownership and
resource update requirements are approved.
