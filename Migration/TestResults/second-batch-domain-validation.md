# Second-batch Domain validation

Date: 2026-08-23

Editor contract: Unity 2022.3.62f3

## Results

| Check | Result | Evidence |
|---|---|---|
| Domain static compilation | PASS | Compiled all `Assets/CompanyWarRE/Domain/*.cs` with Unity's Roslyn compiler and Unity 4.8 reference assemblies. |
| Domain test static compilation | PASS | Compiled all `Assets/CompanyWarRE/Tests/Editor/Domain/*.cs` against Domain and Unity's NUnit assembly. |
| Pure C# behavior execution | PASS | Unity Mono runner executed 23 NUnit test cases: 23 passed, 0 failed. |
| Forbidden dependency scan | PASS | No Unity, UnityEditor, QFramework, UniTask, DOTween, or Addressables token was found in Domain source. |
| Domain asmdef boundary | PASS | `references` is empty and `noEngineReferences` is true. |
| Static Domain assembly references | PASS | Only `mscorlib` and `System.Core`. |
| Unity-imported Domain assembly references | PASS | Only `netstandard`. |
| Cow evidence hashes | PASS | All ten evidence file hashes and byte lengths match the read-only source at validation time. |

## Unity Test Runner

The target editor imported and compiled `CompanyWarRE.Domain.dll` and
`CompanyWarRE.Domain.EditorTests.dll`. The final added cases were validated with the
same Unity Roslyn/Mono toolchain outside the open editor. Run the EditMode suite in
the open editor once to record the UI-level result together with the existing
migration tests.

This report does not claim scene, prefab, ScriptableObject, resource, PlayMode, or
build validation; those remain outside this batch.
