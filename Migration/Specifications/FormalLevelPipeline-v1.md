# Formal level configuration pipeline v1

Status: L02-L05 imported and executable through the compatibility boundary

## Source scope

Only these Cow documents were imported in this batch:

| Level | Macro size | Initial controlled macro rows | Stages | Buildings | Assault target |
| --- | ---: | ---: | ---: | ---: | ---: |
| L02 | 6x10 | 2 | 4 | 8 | 11 |
| L03 | 8x12 | 2 | 4 | 10 | 14 |
| L04 | 8x12 | 2 | 5 | 12 | 17 |
| L05 | 8x15 | 2 | 5 | 15 | 21 |

No Cow level directory, `.meta` collection, scene, prefab, ProjectSettings, or
unrequested L06-L20 document was copied.

## Version and coordinate contract

- Formal level documents require `SchemaVersion = 1`.
- `CoordinateSpace` must be `MacroControlBlock3x3`.
- A source macro grid of `C x R` becomes a runtime small-cell grid of
  `(C * 3) x (R * 3)`.
- Macro coordinate `(c, r)` owns the small-cell footprint beginning at
  `((c - 1) * 3 + 1, (r - 1) * 3 + 1)`.
- Its actor/building center is `((c - 1) * 3 + 2, (r - 1) * 3 + 2)`.
- Initial controlled macro rows use the same scale. L02-L05 therefore start
  with six controlled small-cell rows.
- Spawn columns cover every runtime small-cell column and the default spawn
  row is the last runtime row. Building-aware spawn retreat remains a Domain
  rule.

All conversions go through `FormalLevelCoordinateConverter`; individual level
loaders must not perform their own coordinate arithmetic.

## Per-level configuration

Each document independently defines:

- source dimensions and initial controlled rows;
- initial resources and production cadence;
- selected wave-stage sequence and random seed;
- enemy building types and macro coordinates;
- objective mode, building-victory flag, and required assault score;
- environment ID, decor ring, auto-size values, skyline density/seed, and
  optional scene-prop placements.

Cow L02-L05 omit `Environment`, so v1 explicitly records the values Cow applies
at runtime: `Cow.DefaultIndustrial`, decor ring 5, skyline density 0.65, seed
1001, and automatic outer-ground/skyline distance (`0` means auto-size).

## Validation and error report

`FormalLevelConfigurationPipeline` validates schema, coordinate space,
dimensions, controlled territory, runtime values, unique stages, objective,
environment, scene props, catalog references, schedule references, building
bounds, and overlapping 3x3 footprints. Errors use the existing structured
contract:

`Code + document/field path + message`

`FormalLevelConfigurationPipeline.ValidateBatch` additionally reports each
document separately, rejects duplicate level IDs, and produces an aggregate
error count.

In Unity, run:

`Company War-RE > Migration > Validate Formal Levels L02-L05`

For CI/batch mode, execute:

`CompanyWarRE.EditorTools.FormalLevelBatchValidationTool.ValidateForBatch`

The tool writes `Migration/Reports/FormalLevelValidation.json`. It does not
modify any source configuration.

## Architecture boundary

- Coordinate, grid, combat, wave, and objective behavior remain pure C# Domain.
- Parsing, schema compatibility, mapping, and batch validation live in
  Infrastructure.
- The editor tool only invokes Infrastructure and writes a report.
- QFramework remains the Application flow boundary; no QFramework or Unity
  reference was introduced into Domain.
