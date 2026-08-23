# Battle slice validation

Date: 2026-08-23

Editor contract: Unity 2022.3.62f3

## Results

| Check | Result | Evidence |
|---|---|---|
| Unity assembly compilation | PASS | Unity generated Domain, Application, Presentation, Domain test, Application test, and Migration test assemblies. |
| Scene serialization import | PASS | Unity imported `BattleSliceTest.unity` as artifact `bdb5522cb3f31fb527fdf5d94026d567`. |
| Scene script GUID | PASS | The serialized Bootstrap component resolves to `BattleSliceController.cs.meta`. |
| Application slice tests | PASS | 6 passed, 0 failed using compiled assemblies and Mono runtime, including enemy-region spawn and breach pollution integration. |
| Domain regression tests | PASS | 35 passed, 0 failed using compiled assemblies and Mono runtime; 12 cover combat, reserved battle rows, focus fire, battlefield lifecycle, and territory breach. |
| Infrastructure compatibility tests | PASS | 8 passed, 0 failed; U01/E01 mapping, row-axis composition, deprecated alias, and enemy spawn boundary are covered. |
| Full C# solution compile | PASS | 0 errors; warnings are pre-existing QFramework deprecation/unassigned-field warnings. |
| Assembly boundary inspection | PASS | Domain references only `netstandard`; Application references Domain and QFramework; Presentation owns Unity input, physics, audio, IMGUI, and QFramework references. |
| ProjectSettings changes | PASS | None. The test scene was not added to EditorBuildSettings. |
| Cow modifications/imports | PASS | None. The scene and placeholder visuals are target-owned. |

## Pending manual observation

Open `Assets/CompanyWarRE/Scenes/BattleSliceTest.unity` and enter Play Mode to
confirm the rendered colors and mouse/keyboard feel. Automated checks validate the
rule path and serialized references, but do not judge visual clarity or input
ergonomics. Unity Test Runner should be rerun after the editor finishes importing
the newly added scripts and TextAsset.
