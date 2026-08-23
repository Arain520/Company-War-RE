# Configuration compatibility validation

Date: 2026-08-23

Editor contract: Unity 2022.3.62f3

## Results

| Check | Result | Evidence |
|---|---|---|
| Infrastructure compilation | PASS | Unity-compatible Roslyn compilation completed for production and Editor test assemblies. |
| Compatibility tests | PASS | 6 passed, 0 failed. |
| Application regression tests | PASS | 4 passed, 0 failed after replacing hardcoded setup with `BattleSliceConfiguration`. |
| U01 source parity | PASS | All 14 fields in the target U01 sample match Cow `Units.json`. |
| Runtime-default parity | PASS | Initial resources 10 and production intervals 5/3 match audited Cow runtime setup calls. |
| Source evidence | PASS | Cow `Units.json` SHA-256 is `8c156a406d6e54e551745ebd6bd2451efd38bfc575120d7c9d641b0cd3a696db`, 6516 bytes. |
| Infrastructure dependency boundary | PASS | Compiled references are `netstandard`, Application, and Domain; no Unity or QFramework assembly reference. |
| Storage strategy neutrality | PASS | Provider reads `IConfigurationTextSource` logical keys and contains no StreamingAssets, Resources, ResKit, or Addressables API. |
| Scene configuration references | PASS | Scene YAML records both target-owned TextAsset GUIDs and the existing controller GUID. |

## Covered failures

- Missing/empty source document: `CFG_SOURCE`
- Malformed JSON: `CFG_PARSE`
- Unsupported settings version: `CFG_SCHEMA`
- Missing values: `CFG_REQUIRED`
- Duplicate IDs: `CFG_DUPLICATE`
- Invalid numeric ranges: `CFG_RANGE`
- Missing referenced unit: `CFG_REFERENCE`
- Unsupported legacy enum values: `CFG_ENUM`

The U01 sample is deliberately partial. This report does not claim complete unit,
enemy, authorization, level, spawn, save, or resource-pipeline migration.
