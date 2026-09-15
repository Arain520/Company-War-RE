using System.IO;
using CompanyWarRE.Presentation;
using UnityEditor;
using UnityEngine;

namespace CompanyWarRE.EditorTools
{
    public sealed class FormalAudioConfigurationWindow : EditorWindow
    {
        private const string CatalogAssetPath =
            "Assets/CompanyWarRE/Resources/CompanyWarRE/Presentation/FormalAudioCatalog.asset";

        private FormalAudioCatalog _catalog;
        private SerializedObject _serializedCatalog;

        [MenuItem("Company War/Audio/BGM 与音效配置")]
        public static void Open()
        {
            var window = GetWindow<FormalAudioConfigurationWindow>("音频配置");
            window.minSize = new Vector2(430f, 360f);
            window.Show();
        }

        private void OnEnable()
        {
            LoadCatalog();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Company War 音频配置", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "主界面与选关共用菜单 BGM；关卡 BGM 按 Level Id 匹配。音频留空时运行期保持静音且不会报错。",
                MessageType.Info);

            if (_catalog == null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("尚未创建配置：" + CatalogAssetPath, EditorStyles.wordWrappedLabel);
                if (GUILayout.Button("创建默认音频配置", GUILayout.Height(32f)))
                {
                    EnsureDefaultCatalog();
                    LoadCatalog();
                }
                return;
            }

            _serializedCatalog.Update();
            EditorGUILayout.PropertyField(_serializedCatalog.FindProperty("menuMusic"));
            EditorGUILayout.PropertyField(_serializedCatalog.FindProperty("crossFadeSeconds"));
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_serializedCatalog.FindProperty("levelMusic"), true);
            if (_serializedCatalog.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(_catalog);
                AssetDatabase.SaveAssets();
            }

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("在 Project 中定位"))
                {
                    Selection.activeObject = _catalog;
                    EditorGUIUtility.PingObject(_catalog);
                }
                if (GUILayout.Button("恢复默认关卡项"))
                {
                    if (EditorUtility.DisplayDialog("恢复默认关卡项", "这会重建 L01-L04 项并清除已绑定的关卡音乐。", "恢复", "取消"))
                    {
                        ResetLevelEntries(_serializedCatalog);
                    }
                }
            }
        }

        public static void EnsureDefaultCatalog()
        {
            var existing = AssetDatabase.LoadAssetAtPath<FormalAudioCatalog>(CatalogAssetPath);
            if (existing != null)
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(CatalogAssetPath));
            var catalog = CreateInstance<FormalAudioCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
            var serialized = new SerializedObject(catalog);
            ResetLevelEntries(serialized);
            AssetDatabase.SaveAssets();
        }

        private static void ResetLevelEntries(SerializedObject serialized)
        {
            var entries = serialized.FindProperty("levelMusic");
            entries.arraySize = 4;
            for (var index = 0; index < entries.arraySize; index++)
            {
                var entry = entries.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("levelId").stringValue = "L" + (index + 1).ToString("00");
                entry.FindPropertyRelative("music").objectReferenceValue = null;
            }
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(serialized.targetObject);
            AssetDatabase.SaveAssets();
        }

        private void LoadCatalog()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<FormalAudioCatalog>(CatalogAssetPath);
            _serializedCatalog = _catalog != null ? new SerializedObject(_catalog) : null;
        }
    }
}
