# Company War Domain behavior specification v0.1

Status: second-batch implementation contract

Source project: `D:\UNITY2\Cow` (read only)

Target project: `D:\UNITY\Company War-RE`

Fixed editor: Unity `2022.3.62f3`

## Scope

This batch creates an executable behavior specification and a pure C# Domain
skeleton. It does not migrate scenes, prefabs, ScriptableObjects, StreamingAssets,
or production game resources. It also does not select a final resource update
pipeline.

QFramework may later connect Application, Infrastructure, and Presentation. Domain
does not reference QFramework or Unity and does not choose Addressables, ResKit,
StreamingAssets, UniTask, or DOTween policy.

## Architecture boundary

```
Presentation (Unity/QFramework) -> Application -> Domain
Infrastructure (save/config/assets) ---------> Application/Domain ports
```

Domain owns deterministic rules and values. Unity components translate input,
time, scene objects, and visual effects at the boundary. Configuration loading and
save serialization remain Infrastructure responsibilities.

## Locked compatibility behavior

| ID | Behavior | Target contract |
|---|---|---|
| GRID-001 | Coordinates | Grid positions are one-based. Invalid positions do not throw during queries. |
| GRID-002 | Control blocks | A control block covers up to 3 x 3 small cells; partial edge blocks are allowed. |
| GRID-003 | Pollution | Current runtime behavior applies pollution to every cell in the selected control block and records only state changes. |
| GRID-004 | Territory | A control block is controlled when at least one cell is owned and not polluted. |
| DEPLOY-001 | Standard deployment | The target cell must exist, be owned, unpolluted, and empty. Standard placement capacity is one actor per cell. |
| DEPLOY-002 | Cost/cooldown | A successful deployment spends resources and starts a cooldown keyed by unit ID. Rejections expose a stable reason. |
| RES-001 | Initialization | Negative initial resources clamp to zero. Non-positive gains are ignored. |
| RES-002 | Production | Default fixed production is +1 every 5 seconds. Transmitters produce their configured amounts every 3 seconds. Intervals are configurable. |
| RES-003 | Spending | Positive spend succeeds only when affordable. Legacy non-positive spend is a successful no-op. |
| AUTH-001 | Capacity | The deploy list contains at most 12 unique, case-insensitive unit IDs. |
| AUTH-002 | Thresholds | The first observed requirement is 7 points and the next same-stage requirement is 14 after accepting a choice. |
| AUTH-003 | Candidate counts | Stage 0 requests up to 4 candidates, stage 1 up to 3, and later stages up to 2. |
| OUTCOME-001 | Territory defeat | No controlled, unpolluted territory means defeat. |
| OUTCOME-002 | Spawn defeat | When enemy-building victory is disabled, no valid spawn point means defeat. |
| OUTCOME-003 | Building victory | When enemy-building victory is enabled, zero enemy buildings means victory. |

## Conflicts requiring approval

| Decision | Current evidence | Provisional target behavior |
|---|---|---|
| Pollution granularity | Current `GridMap.SetPollution` changes the whole 3 x 3 block. `CW_UNIT_0101_GridMap` appears to expect cell-local pollution and is inconsistent with the implementation. | Preserve current runtime block-wide behavior. Do not treat the stale test as authoritative unless playtest evidence overturns this. |
| Assault-score victory | Configuration contains `RequiredAssaultScore`, but the audited battle outcome path does not award victory from that score. | Characterize current runtime: score alone does not win. Product decision remains open. |

## Explicitly deferred behavior

- Weighted/random authorization selection, seed compatibility, and the special U15
  early-choice guarantee. The Domain exposes `IAuthorizationCandidateSelector` so
  this policy can be added without Unity randomness.
- Building, support-effect, terrain-build, and outer-ring deployment. The skeleton
  rejects these with `UnsupportedMode` instead of silently approximating them.
- Combat resolution, projectiles, targeting, AI, spawning, and presentation timing.
- Save/config DTOs and migrations. Unity types such as `Vector2` will not enter Domain.
- Completeness and authoritative source for units U25-U36, which appear outside the
  audited primary units configuration.
- Whether transmitter production time should accumulate while no transmitter is
  registered. The skeleton preserves the observed legacy timer behavior.
- Whether a non-positive resource spend should remain a successful no-op after the
  compatibility phase.

## Executable examples

The Editor tests under `Assets/CompanyWarRE/Tests/Editor/Domain` characterize the
contract without requiring migrated scenes or resources. A behavior becomes
"approved" only after its row is accepted and the matching test passes in the fixed
Unity editor.

## Definition of done for this batch

- The Domain assembly compiles with no Unity or QFramework references.
- Characterization tests compile and are discoverable as EditMode tests.
- Every implemented rule has Cow source/test evidence in the traceability table.
- Conflicts and deferred behaviors fail closed or remain behind an explicit port.
- No Cow file is modified and no scene/resource is migrated.
