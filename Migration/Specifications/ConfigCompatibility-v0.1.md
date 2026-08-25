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

Territory is composed along the row axis: rows `1..ControlledRows` belong to the
ally and enemy spawn rows must be greater than `ControlledRows`. The earlier
`ControlledColumns` test setting is retained as a deprecated read alias so existing
v1 text does not fail abruptly, but new composition must write `ControlledRows`.

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
- A level whose dimensions already match the target grid uses small-cell coordinates
  unchanged. A Cow level whose dimensions expand to the target by exactly `3x` is
  treated as a macro/control-block grid: dimensions are multiplied by three and
  every building coordinate maps to `((source - 1) * 3) + 2`.
- When a level supplies `Stages`, only those names are selected from the shared spawn
  schedule document, in the level's declared order. Missing names are reference
  errors.
- Unsupported schema versions, enum values, references, and ranges return stable
  issues instead of constructing a partial Domain configuration.

## Current sample versus full migration

`LegacyUnits.U01.json` remains a one-record unit sample. `LegacyEnemies.E01.json`
currently contains the combatants needed by the test slices, including E01-E07 and
E12-E15, but is not yet the complete enemy catalog. Remaining units, enemies,
authorization, and later levels remain later batches.

`LegacyLevel.L01.json` is the first concrete imported Cow level. Its source values
remain at the legacy `6x10` macro-grid scale; `BattleSliceRuntime.L01.json` is the
target-owned adapter composition that declares the resulting `18x30` small-cell
grid, six controlled small rows, and existing runtime defaults. The executable test
scene references this pair. Storage and resource-update strategy remain replaceable.

The older `BattleSliceRuntime.json` and `LegacyLevel.BattleSlice.json` remain as a
small target-owned characterization fixture and are no longer the executable scene
composition.
