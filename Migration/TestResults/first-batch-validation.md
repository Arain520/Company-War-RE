# First-batch validation report

Date: 2026-08-23

## Passed checks

- Cow recoverable snapshot: Git bundle verification passed.
- Cow working-tree restore: 1,803 files extracted and matched by SHA-256.
- Cow source protection: current porcelain-v2 Git status exactly matched the pre-snapshot capture.
- Inventory generation: completed without importing source assets into the target `Assets` tree.
- Test skeleton static compilation: four C# test sources compiled successfully against Unity 2022.3.62f3 reference assemblies.
- Static test assembly SHA-256: `e669b4e050b0bc1b8ec0856ad83303037513bf630f9405b69e401b508906dd6d`.

## Unity Test Runner status

EditMode execution was attempted with `D:\UNITY2\Editor\Unity.exe` (2022.3.62f3), first against the target and then against an isolated temporary target copy. Both attempts stopped before project loading because existing Unity processes held the global cache database:

`C:\Users\user\AppData\Local\Unity\Caches\CurlRequestCache.db`

Unity raised a native exception while initializing `CurlFileCache`. No test case ran, so this is recorded as **not executed**, not as a test failure. The existing Unity processes and global cache were left untouched. Re-run the EditMode suite after other Unity processes release the cache.

## Scope confirmation

- No Cow scene, prefab, ScriptableObject, production script, or gameplay resource was copied into target `Assets`.
- No dependency or render-pipeline change was made.
- No file under `D:\UNITY2\Cow` was modified.

## Interactive Test Runner follow-up

An interactive EditMode run completed eight tests and exposed two test-definition false positives:

- The Missing Script scan included a third-party ResKit example scene outside Build Settings and project-owned asset roots.
- The serialization manifest check did not support escaped double quotes in valid CSV fields.

Both tests were corrected. The updated four-file test assembly compiles against Unity 2022.3.62f3, all 409 serialization rows pass the corrected CSV contract, and the ResKit example scene is excluded while `Assets/Scenes/SampleScene.unity` remains covered. An interactive rerun is still required to record the final Unity Test Runner result.
