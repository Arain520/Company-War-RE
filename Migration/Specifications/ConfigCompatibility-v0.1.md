# Configuration compatibility layer v0.1

Status: first executable compatibility slice

## Scope

This layer reads the legacy `Units.json` document shape, validates it, and maps one
selected unit into the pure C# Domain model. The first target-owned sample preserves
Cow U01 values: resource cost 1 and deploy cooldown 3 seconds.

The test runtime settings preserve separately audited code defaults where relevant:
initial resources 10, fixed production every 5 seconds, and transmitter production
every 3 seconds. They are not presented as fields from Cow `Units.json`.

## Boundary

```
IConfigurationTextSource
    -> Legacy JSON DTOs
    -> validation with stable issue codes
    -> BattleSliceConfiguration + UnitDefinition
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
- Resource cost and deploy cooldown cannot be negative.
- Legacy `Staff`, `Building`, and `Support` types map to explicit Domain deployment
  modes. `Support` with `TerrainBuild` maps to `TerrainBuild`; other support effects
  map to `SupportEffect`.
- Missing footprint values retain the legacy default `SmallCell` behavior.
- Unknown JSON fields are tolerated for forward compatibility.
- Unsupported schema versions, enum values, references, and ranges return stable
  issues instead of constructing a partial Domain configuration.

## Current sample versus full migration

`LegacyUnits.U01.json` is a traceable one-unit compatibility sample, not the final
or complete migrated `Units.json`. U02-U24, enemies, authorization, levels, spawn
schedules, and U25-U36 completeness remain later batches.

`BattleSliceRuntime.json` is target-owned test composition. It is versioned with
`SchemaVersion: 1` and may be replaced when production configuration ownership and
resource update requirements are approved.
