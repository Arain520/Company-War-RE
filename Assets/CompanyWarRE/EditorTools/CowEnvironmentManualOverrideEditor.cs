using CompanyWarRE.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CompanyWarRE.EditorTools
{
    public static class CowEnvironmentManualOverrideEditor
    {
        private const string CommandPath =
            "GameObject/Company War-RE/将所选环境对象转为手工对象";

        [MenuItem(CommandPath, false, 20)]
        private static void ConvertSelectedToManual()
        {
            var selected = Selection.activeTransform;
            if (!TryResolveGeneratedUnit(selected, out var environment, out var generatedUnit))
            {
                Debug.LogWarning(
                    "请先选择 BakedCowEnvironment 下的栏杆、管线、楼梯、远景建筑或环境模型。",
                    selected);
                return;
            }

            var editable = environment.transform.Find("EditableEnvironmentModelFill");
            if (editable == null)
            {
                var editableObject = new GameObject("EditableEnvironmentModelFill");
                Undo.RegisterCreatedObjectUndo(editableObject, "创建手工环境根节点");
                editable = editableObject.transform;
                Undo.SetTransformParent(editable, environment.transform, "创建手工环境根节点");
                editable.localPosition = Vector3.zero;
                editable.localRotation = Quaternion.identity;
                editable.localScale = Vector3.one;
                editable.gameObject.layer = FormalBattleMapView.EnvironmentLayer;
            }

            Undo.RecordObject(environment, "记录环境生成排除项");
            environment.ExcludeGeneratedObject(generatedUnit.name);
            Undo.SetTransformParent(generatedUnit, editable, "转为手工环境对象");
            generatedUnit.SetAsLastSibling();
            Selection.activeTransform = generatedUnit;

            EditorUtility.SetDirty(environment);
            EditorUtility.SetDirty(generatedUnit);
            EditorSceneManager.MarkSceneDirty(generatedUnit.gameObject.scene);
            Debug.Log(
                $"'{generatedUnit.name}' 已转为手工环境对象；以后重新烘焙不会再生成同名原件。" +
                "请保存 Prefab，并重新进入 Play Mode 查看。",
                generatedUnit);
        }

        [MenuItem(CommandPath, true)]
        private static bool ValidateConvertSelectedToManual()
        {
            return TryResolveGeneratedUnit(
                Selection.activeTransform,
                out _,
                out _);
        }

        private static bool TryResolveGeneratedUnit(
            Transform selected,
            out CowIndustrialEnvironmentView environment,
            out Transform generatedUnit)
        {
            environment = null;
            generatedUnit = null;
            if (selected == null)
            {
                return false;
            }

            environment = selected.GetComponentInParent<CowIndustrialEnvironmentView>(true);
            if (environment == null)
            {
                return false;
            }

            var baked = environment.transform.Find("BakedCowEnvironment");
            if (baked == null || selected == baked || !selected.IsChildOf(baked))
            {
                return false;
            }

            var seeded = baked.Find("SeededEnvironmentModelFill");
            var boundary = seeded != null && selected.IsChildOf(seeded) ? seeded : baked;
            var candidate = selected;
            while (candidate.parent != null && candidate.parent != boundary)
            {
                candidate = candidate.parent;
            }

            if (candidate.parent != boundary)
            {
                return false;
            }

            generatedUnit = candidate;
            return true;
        }
    }
}
