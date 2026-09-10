using System;
using System.IO;
using CompanyWarRE.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CompanyWarRE.EditorTools
{
    /// <summary>Real-engine smoke check and a reproducible composition render.</summary>
    public static class SkyBattlefieldVerification
    {
        public const string ResultFolder = "Migration/Artifacts/SkyBattlefield";

        [MenuItem("Company War-RE/验证并渲染云上战场")]
        public static void Run()
        {
            Directory.CreateDirectory(ResultFolder);
            var map = AssetDatabase.LoadAssetAtPath<GameObject>(SkyBattlefieldPrefabGeneratorWindow.DefaultPrefabPath);
            if (map == null) map = SkyBattlefieldPrefabGeneratorWindow.Generate(new SkyBattlefieldPrefabGeneratorWindow.Settings()).gameObject;
            Require(map.GetComponentInChildren<FormalBattleBoardView>(true) == null, "Map must not bake the playable board.");
            Require(map.GetComponentsInChildren<Collider>(true).Length == 0, "Environment must not intercept input.");
            var previousScene = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var previousOpacity = Shader.GetGlobalFloat("_CompanyWarCloudOpacityScale");
            var previousAsyncCompilation = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            RenderTexture target = null;
            Texture2D image = null;
            GameObject instance = null;
            try
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(map, scene);
                var mapView = instance.GetComponent<FormalBattleMapView>();
                var board = new GameObject("VerificationDynamicBoard").AddComponent<FormalBattleBoardView>();
                board.transform.SetParent(mapView.BattleBoardAnchor, false);
                instance.GetComponent<SkyBattlefieldSettings>().ApplyTo(board);
                var randomState = UnityEngine.Random.state;
                board.Prepare(mapView.PreviewColumns, mapView.PreviewRows);
                Require(UnityEngine.Random.state.Equals(randomState), "Pillar generation must preserve global random state.");
                var expectedPillars = Mathf.CeilToInt(mapView.PreviewColumns / 3f) * Mathf.CeilToInt(mapView.PreviewRows / 3f);
                Require(board.PillarRoot.childCount == expectedPillars, "Incorrect runtime pillar count.");
                Require(board.CoordinateMapper.MaximumPillarTopY > board.CoordinateMapper.MinimumPillarTopY, "Pillars need real height variation.");
                var clouds = mapView.EnvironmentRoot.Find("BakedCloudSea");
                var count = mapView.EnvironmentRoot.GetComponentsInChildren<Transform>(true).Length;
                var atmosphere = mapView.PrepareForBattle(mapView.PreviewColumns, mapView.PreviewRows);
                mapView.PrepareForBattle(mapView.PreviewColumns, mapView.PreviewRows, board.CoordinateMapper);
                Require(mapView.EnvironmentRoot.Find("BakedCloudSea") == clouds, "Baked clouds were replaced.");
                Require(mapView.EnvironmentRoot.GetComponentsInChildren<Transform>(true).Length == count, "Runtime rebuilt static environment.");
                board.PillarGenerator.ConfigureCloudAbyss(atmosphere.MinimumPillarBottomY,
                    atmosphere.HeightFogTopY, atmosphere.HeightFogBottomY, atmosphere.HeightFogColor,
                    atmosphere.HeightFogStrength, atmosphere.HeightFogCurve);
                foreach (var renderer in mapView.EnvironmentRoot.GetComponentsInChildren<Renderer>())
                {
                    Require(renderer.enabled, "Atmosphere hid authored architecture.");
                    foreach (var material in renderer.sharedMaterials)
                        Require(material != null && AssetDatabase.Contains(material), "Environment material was not persisted.");
                }
                foreach (var pillar in board.PillarGenerator.Pillars.Values)
                {
                    var model = pillar.transform.Find("SkyPillarModel");
                    Require(model != null, "Pillar did not use Content model.");
                    var bounds = model.GetComponentInChildren<Renderer>().bounds;
                    Require(bounds.size.y > bounds.size.x * 3f && bounds.size.y > bounds.size.z * 3f,
                        "Imported pillar is not upright; preserve the FBX axis conversion.");
                    Require(Mathf.Abs(model.TransformPoint(new Vector3(0f, 16f, 0f)).y - pillar.BuildAnchor.position.y) < 0.002f,
                        "Model deck and gameplay anchor differ.");
                }
                var camera = new GameObject("VerificationCamera").AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = Mathf.Max(board.PresentationWidth, board.PresentationLength) * 0.78f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 2400f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.77f, 0.83f, 0.89f);
                camera.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
                camera.transform.position = new Vector3(0f, board.CoordinateMapper.AveragePillarTopY - 20f, 0f)
                    - camera.transform.forward * 500f;
                var data = camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                data.requiresDepthTexture = true;
                data.volumeLayerMask = ~0;
                target = new RenderTexture(1600, 1000, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = target;
                var ambientProbe = new UnityEngine.Rendering.SphericalHarmonicsL2();
                ambientProbe.AddAmbientLight(new Color(0.52f, 0.60f, 0.70f));
                RenderSettings.ambientProbe = ambientProbe;
                Shader.SetGlobalFloat("_CompanyWarCloudOpacityScale", 1f);
                camera.Render();
                Require(!ShaderUtil.ShaderHasError(Shader.Find("CompanyWarRE/SkyArchitectureLitURP")), "Architecture shader failed compilation.");
                Require(!ShaderUtil.ShaderHasError(Shader.Find("CompanyWarRE/CloudSeaURP")), "Cloud shader failed compilation.");
                var previousTarget = RenderTexture.active;
                try
                {
                    RenderTexture.active = target;
                    image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                    image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                    image.Apply();
                    File.WriteAllBytes(ResultFolder + "/SkyBattlefield_Unity.png", image.EncodeToPNG());
                }
                finally { RenderTexture.active = previousTarget; }
                board.Prepare(6, 6);
                Require(board.PillarRoot.childCount == 4, "Repeated Prepare left duplicate pillars.");
                File.WriteAllText(ResultFolder + "/verification.txt",
                    "PASS: generated and reloaded prefab; no baked playable board; no environment colliders; " +
                    "all environment materials persisted; static environment survives runtime preparation; " +
                    "Content models render; pillar deck/build anchors align after extension; deterministic real height variation; " +
                    "global random state preserved; runtime rebuild removes old pillars.\n" +
                    "Default runtime pillar count: " + expectedPillars + "\nUnity: " + UnityEngine.Application.unityVersion);
                Debug.Log("Sky battlefield verification passed. " + ResultFolder);
            }
            finally
            {
                if (instance != null) Object.DestroyImmediate(instance);
                if (target != null) Object.DestroyImmediate(target);
                if (image != null) Object.DestroyImmediate(image);
                EditorSceneManager.CloseScene(scene, true);
                SceneManager.SetActiveScene(previousScene);
                Shader.SetGlobalFloat("_CompanyWarCloudOpacityScale", previousOpacity);
                ShaderUtil.allowAsyncCompilation = previousAsyncCompilation;
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
