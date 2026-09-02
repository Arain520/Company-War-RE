using System.Collections.Generic;
using System.Linq;
using CompanyWarRE.Presentation;
using UnityEditor;
using UnityEngine;

namespace CompanyWarRE.EditorTools
{
    [InitializeOnLoad]
    public static class CowIndustrialEnvironmentPrefabBaker
    {
        private const string MaterialFolder =
            "Assets/CompanyWarRE/Content/Environment/Cow/Materials";
        private const string BaseMapPath =
            "Assets/CompanyWarRE/Content/Maps/PF_BaseFormalBattleMap.prefab";
        private const string SessionKey = "CompanyWarRE.CowEnvironmentPrefabBake.v11";
        private const float TargetEnvironmentExtentMultiplier = 8f;
        private const int TargetSkylineBlockCount = 96;
        private const float GroundPatternWorldSize = 32f;

        static CowIndustrialEnvironmentPrefabBaker()
        {
            EditorApplication.delayCall += BakePendingKnownMaps;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.delayCall += BakePendingKnownMaps;
            }
        }

        public static void BakeInMemory(FormalBattleMapView map, int columns, int rows)
        {
            if (map == null || map.EnvironmentRoot == null)
            {
                return;
            }

            var environment = map.EnvironmentRoot
                .GetComponentInChildren<CowIndustrialEnvironmentView>(true);
            if (environment == null)
            {
                return;
            }

            environment.ConfigureEnvironmentExtent(
                TargetEnvironmentExtentMultiplier,
                TargetSkylineBlockCount);
            RegisterManualExclusions(environment);
            var previous = environment.transform.Find("BakedCowEnvironment");
            if (previous != null)
            {
                Object.DestroyImmediate(previous.gameObject);
            }

            environment.Build(columns, rows);
            var generated = environment.transform.Find("GeneratedCowEnvironment");
            if (generated == null)
            {
                return;
            }

            generated.name = "BakedCowEnvironment";
            EnsureEditableFillRoot(environment.transform);
            environment.RefreshManualImportedMaterials();
            PersistMaterials(environment.transform);
            ResizeGround(map.EnvironmentRoot, columns, rows, environment.EnvironmentExtentMultiplier);
            EditorUtility.SetDirty(map);
            EditorUtility.SetDirty(environment);
        }

        [MenuItem("Company War-RE/地图/烘焙全部 Cow 环境 Prefab")]
        public static void BakeAllKnownMaps()
        {
            BakePrefabAtPath(BaseMapPath, true);
            const string contentMaps = "Assets/CompanyWarRE/Content/Maps";
            if (AssetDatabase.IsValidFolder(contentMaps))
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { contentMaps }))
                {
                    BakePrefabAtPath(AssetDatabase.GUIDToAssetPath(guid), true);
                }
            }
            AssetDatabase.SaveAssets();
        }

        private static void BakePendingKnownMaps()
        {
            if (SessionState.GetBool(SessionKey, false) ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (EditorApplication.isCompiling)
            {
                EditorApplication.delayCall += BakePendingKnownMaps;
                return;
            }

            SessionState.SetBool(SessionKey, true);
            BakePrefabAtPath(BaseMapPath, false);
            AssetDatabase.SaveAssets();
        }

        private static void BakePrefabAtPath(string path, bool force)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                return;
            }

            var assetMap = prefab.GetComponent<FormalBattleMapView>();
            if (assetMap == null || assetMap.EnvironmentRoot == null)
            {
                return;
            }

            var assetEnvironment = assetMap.EnvironmentRoot
                .GetComponentInChildren<CowIndustrialEnvironmentView>(true);
            if (assetEnvironment == null)
            {
                return;
            }

            var baked = assetEnvironment.transform.Find("BakedCowEnvironment");
            var ground = assetMap.EnvironmentRoot.Find("BaseGround");
            var expectedSide = Mathf.Max(assetMap.PreviewColumns, assetMap.PreviewRows) *
                               TargetEnvironmentExtentMultiplier;
            var currentBake = baked != null &&
                              baked.Find("SeededEnvironmentModelFill") != null &&
                              assetEnvironment.transform.Find("EditableEnvironmentModelFill") != null &&
                              ManualExclusionsAreSynchronized(assetEnvironment) &&
                              ImportedMaterialsArePreserved(baked) &&
                              Mathf.Abs(assetEnvironment.EnvironmentExtentMultiplier -
                                        TargetEnvironmentExtentMultiplier) < 0.001f &&
                              GroundUsesIndustrialPanelMaterial(ground) &&
                              ground != null &&
                              Mathf.Abs(ground.localScale.x - expectedSide) < 0.001f &&
                              Mathf.Abs(ground.localScale.z - expectedSide) < 0.001f;
            if (!force && currentBake)
            {
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var map = root.GetComponent<FormalBattleMapView>();
                BakeInMemory(map, map.PreviewColumns, map.PreviewRows);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void PersistMaterials(Transform environmentRoot)
        {
            EnsureFolder(MaterialFolder);
            var persistentByRuntime = new Dictionary<Material, Material>();
            foreach (var renderer in environmentRoot.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                var changed = false;
                for (var slot = 0; slot < materials.Length; slot++)
                {
                    var runtimeMaterial = materials[slot];
                    if (runtimeMaterial == null || AssetDatabase.Contains(runtimeMaterial))
                    {
                        continue;
                    }

                    materials[slot] = GetOrCreatePersistentMaterial(
                        runtimeMaterial,
                        persistentByRuntime);
                    changed = true;
                }

                if (changed)
                {
                    renderer.sharedMaterials = materials;
                    EditorUtility.SetDirty(renderer);
                }
            }
        }

        private static Material GetOrCreatePersistentMaterial(
            Material runtimeMaterial,
            IDictionary<Material, Material> persistentByRuntime)
        {
            if (persistentByRuntime.TryGetValue(runtimeMaterial, out var persistent))
            {
                return persistent;
            }

            var imported = runtimeMaterial.name.StartsWith("CowImported_", System.StringComparison.Ordinal);
            var prefix = imported ? "MAT_CowImported_" : "MAT_CowIndustrial_";
            var readableName = imported
                ? SanitizeAssetName(runtimeMaterial.name.Substring("CowImported_".Length))
                : BuildProceduralSuffix(runtimeMaterial);
            var signature = BuildMaterialSignature(runtimeMaterial);
            var suffix = imported
                ? readableName + "_" + Hash128.Compute(signature).ToString().Substring(0, 12)
                : readableName;
            var assetName = prefix + suffix;
            var path = $"{MaterialFolder}/{assetName}.mat";
            persistent = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (persistent == null)
            {
                persistent = new Material(runtimeMaterial) { name = assetName };
                AssetDatabase.CreateAsset(persistent, path);
            }
            else
            {
                EditorUtility.CopySerialized(runtimeMaterial, persistent);
                persistent.name = assetName;
                EditorUtility.SetDirty(persistent);
            }

            persistentByRuntime.Add(runtimeMaterial, persistent);
            return persistent;
        }

        private static string BuildProceduralSuffix(Material material)
        {
            var color = material.HasProperty("_BaseColor")
                ? material.GetColor("_BaseColor")
                : material.color;
            var smoothness = material.HasProperty("_Smoothness")
                ? material.GetFloat("_Smoothness")
                : 0f;
            return ColorUtility.ToHtmlStringRGB(color) + "_" +
                   Mathf.RoundToInt(smoothness * 100f);
        }

        private static string BuildMaterialSignature(Material material)
        {
            var texture = material.HasProperty("_BaseMap")
                ? material.GetTexture("_BaseMap")
                : null;
            var textureIdentity = texture != null &&
                                  AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                                      texture,
                                      out var guid,
                                      out long localId)
                ? guid + ":" + localId
                : texture != null ? texture.name : "none";
            return material.name + "|" + BuildProceduralSuffix(material) + "|" +
                   textureIdentity + "|" + material.shader.name;
        }

        private static string SanitizeAssetName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Material";
            foreach (var invalid in System.IO.Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }
            return value.Trim().Replace('/', '_').Replace('\\', '_');
        }

        private static bool ImportedMaterialsArePreserved(Transform baked)
        {
            var seeded = baked != null ? baked.Find("SeededEnvironmentModelFill") : null;
            return seeded != null && seeded.GetComponentsInChildren<Renderer>(true)
                .SelectMany(renderer => renderer.sharedMaterials)
                .Any(material => material != null &&
                                 material.name.StartsWith(
                                     "MAT_CowImported_",
                                     System.StringComparison.Ordinal));
        }

        private static void EnsureEditableFillRoot(Transform environment)
        {
            var editable = environment.Find("EditableEnvironmentModelFill");
            if (editable != null)
            {
                return;
            }

            var root = new GameObject("EditableEnvironmentModelFill").transform;
            root.SetParent(environment, false);
            root.gameObject.layer = FormalBattleMapView.EnvironmentLayer;
            EditorUtility.SetDirty(root);
        }

        private static void RegisterManualExclusions(CowIndustrialEnvironmentView environment)
        {
            var editable = environment.transform.Find("EditableEnvironmentModelFill");
            if (editable == null)
            {
                return;
            }

            Undo.RecordObject(environment, "记录手工环境排除项");
            var changed = false;
            for (var index = 0; index < editable.childCount; index++)
            {
                changed |= environment.ExcludeGeneratedObject(editable.GetChild(index).name);
            }

            if (changed)
            {
                EditorUtility.SetDirty(environment);
            }
        }

        private static bool ManualExclusionsAreSynchronized(
            CowIndustrialEnvironmentView environment)
        {
            var editable = environment.transform.Find("EditableEnvironmentModelFill");
            if (editable == null)
            {
                return false;
            }

            for (var index = 0; index < editable.childCount; index++)
            {
                if (!environment.ExcludedGeneratedObjectNames.Contains(editable.GetChild(index).name))
                {
                    return false;
                }
            }

            return true;
        }

        private static void ResizeGround(
            Transform environmentRoot,
            int columns,
            int rows,
            float extentMultiplier)
        {
            var ground = environmentRoot.Find("BaseGround");
            if (ground == null)
            {
                return;
            }

            var multiplier = Mathf.Max(2f, extentMultiplier);
            var side = Mathf.Max(60f, Mathf.Max(columns, rows) * multiplier);
            var width = side;
            var length = side;
            ground.localScale = new Vector3(
                width,
                ground.localScale.y,
                length);
            ApplyIndustrialGroundMaterial(ground, width, length);
            EditorUtility.SetDirty(ground);
        }

        private static void ApplyIndustrialGroundMaterial(
            Transform ground,
            float width,
            float length)
        {
            EnsureFolder(MaterialFolder);
            const string texturePath =
                MaterialFolder + "/TEX_CowIndustrialGroundPanels.asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
            {
                texture = new Texture2D(128, 128, TextureFormat.RGBA32, true, false)
                {
                    name = "TEX_CowIndustrialGroundPanels"
                };
                AssetDatabase.CreateAsset(texture, texturePath);
            }
            else if (texture.width != 128 || texture.height != 128)
            {
                texture.Reinitialize(128, 128, TextureFormat.RGBA32, true);
            }

            var pixels = new Color[128 * 128];
            var palette = new[]
            {
                new Color(0.55f, 0.57f, 0.56f),
                new Color(0.62f, 0.63f, 0.60f),
                new Color(0.48f, 0.51f, 0.51f),
                new Color(0.68f, 0.69f, 0.65f),
                new Color(0.43f, 0.47f, 0.48f),
                new Color(0.59f, 0.61f, 0.59f)
            };
            var seam = new Color(0.19f, 0.22f, 0.23f);
            var major = new Color(0.92f, 0.67f, 0.08f);
            for (var y = 0; y < 128; y++)
            {
                for (var x = 0; x < 128; x++)
                {
                    var majorSeam = x < 2 || y < 2;
                    var panelSeam = x % 32 < 1 || y % 32 < 1;
                    var panelX = x / 32;
                    var panelY = y / 32;
                    var color = majorSeam
                        ? major
                        : panelSeam
                            ? seam
                            : palette[(panelX * 3 + panelY * 5 + panelX * panelY) %
                                      palette.Length];
                    pixels[y * 128 + x] = color;
                }
            }

            texture.SetPixels(pixels);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;
            texture.anisoLevel = 4;
            texture.Apply(true, false);
            EditorUtility.SetDirty(texture);

            var materialName = "MAT_CowIndustrialGround_" +
                               Mathf.RoundToInt(width) + "x" + Mathf.RoundToInt(length);
            var materialPath = MaterialFolder + "/" + materialName + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (material == null)
            {
                material = new Material(shader) { name = materialName };
                AssetDatabase.CreateAsset(material, materialPath);
            }

            material.shader = shader;
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", texture);
            material.SetTextureScale(
                "_BaseMap",
                new Vector2(width / GroundPatternWorldSize, length / GroundPatternWorldSize));
            material.SetFloat("_Smoothness", 0.12f);
            EditorUtility.SetDirty(material);

            var renderer = ground.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = true;
                EditorUtility.SetDirty(renderer);
            }
        }

        private static bool GroundUsesIndustrialPanelMaterial(Transform ground)
        {
            var material = ground != null ? ground.GetComponent<Renderer>()?.sharedMaterial : null;
            return material != null &&
                   material.name.StartsWith(
                       "MAT_CowIndustrialGround_",
                       System.StringComparison.Ordinal) &&
                   material.HasProperty("_BaseMap") &&
                   material.GetTexture("_BaseMap") != null;
        }

        private static void EnsureFolder(string path)
        {
            var segments = path.Split('/');
            var current = segments[0];
            for (var index = 1; index < segments.Length; index++)
            {
                var next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }
                current = next;
            }
        }
    }
}
