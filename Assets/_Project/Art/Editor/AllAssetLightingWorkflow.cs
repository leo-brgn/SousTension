using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace SousTension.EditorTools
{
    /// <summary>Real Unity lightmap baking with saved-data verification, followed by native captures.</summary>
    public static class AllAssetLightingWorkflow
    {
        [Serializable] public sealed class BakeReport { public int prefabCount, sceneCount; public BakeResult[] scenes; }
        [Serializable] public sealed class BakeResult
        {
            public string path, kit, lightingData, backend;
            public int prefabCount, lightmapCount, lightmappedRenderers, bakedLights, runtimeLights, probeCount;
            public long atlasTexels;
            public double seconds;
        }
        private const string SettingsRoot = "Assets/_Project/Art/Lighting/";
        private const string ReportPath = "docs/art/ALL_ASSET_LIGHTING_BAKE.json";

        [MenuItem("Sous Tension/Art/Build, bake and capture all assets")]
        public static void Run()
        {
            AllAssetSceneBuilder.Build();
            Bake();
            Capture();
            Debug.Log("ALL_ASSET_LIGHTING_WORKFLOW_PASS");
        }
        public static void Capture()
        {
            var shader = Shader.Find("SousTension/VertexColorLit");
            if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Vertex-color shader has compilation errors");
            var manifest = JsonUtility.FromJson<AllAssetSceneBuilder.SceneManifest>(File.ReadAllText("scratch_out/all-asset-scenes.json"));
            foreach (var entry in manifest.scenes.Where(e => e.kit != "BoatEnvironment"))
            {
                var scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);
                var gallery = scene.GetRootGameObjects().First(g => g.name.StartsWith("Gallery_"));
                var renderers = gallery.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && !(r is ParticleSystemRenderer)).ToArray();
                var bounds = new Bounds(gallery.transform.position, Vector3.one);
                if (renderers.Length > 0) { bounds = renderers[0].bounds; foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds); }
                var camera = Components<Camera>(scene).First(c => c.name == "MainCamera");
                AllAssetSceneBuilder.ReframeCamera(camera, bounds);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            AllAssetSceneCapture.Run();
        }
        // Upgrade existing scenes after visual QA, preserving placement and prefab coverage.
        public static void ImproveAndRun()
        {
            var manifest = JsonUtility.FromJson<AllAssetSceneBuilder.SceneManifest>(File.ReadAllText("scratch_out/all-asset-scenes.json"));
            var emission = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Showcase/WarmLens.mat");
            emission.SetColor("_EmissionColor", new Color(1,.81f,.52f) * 5f); EditorUtility.SetDirty(emission);
            foreach (var entry in manifest.scenes)
            {
                var scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);
                if (entry.kit != "BoatEnvironment")
                {
                    if (!Components<Light>(scene).Any(l => l.name == "BakedGalleryFill")) AllAssetSceneBuilder.AddBakedFill();
                }
                else
                {
                    foreach (var light in Components<Light>(scene).Where(l => l.enabled && l.type == LightType.Point)) light.intensity = 18f;
                }
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets(); Bake(); Capture();
            Debug.Log("ALL_ASSET_LIGHTING_WORKFLOW_PASS");
        }
        public static void Bake()
        {
            BakeEntries(null);
        }
        public static void RepairTerrainAndRun()
        {
            var manifest = JsonUtility.FromJson<AllAssetSceneBuilder.SceneManifest>(File.ReadAllText("scratch_out/all-asset-scenes.json"));
            var affected = manifest.scenes.Where(e => e.path.EndsWith("World_06.unity") || e.path.EndsWith("World_07.unity") || e.kit == "Living").ToArray();
            AllAssetSceneBuilder.PrepareStaticModelUVs(affected.SelectMany(e => e.prefabPaths).ToArray());
            foreach (var entry in affected)
            {
                var scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);
                var gallery = scene.GetRootGameObjects().First(g => g.name.StartsWith("Gallery_"));
                AllAssetSceneBuilder.ConfigureRenderers(gallery, entry.kit);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            BakeEntries(affected.Select(e => e.path).ToArray()); Capture();
            Debug.Log("ALL_ASSET_LIGHTING_WORKFLOW_PASS");
        }
        private static void BakeEntries(string[] selected)
        {
            Directory.CreateDirectory(SettingsRoot);
            AssetDatabase.Refresh();
            OptimizePipeline();
            var profile = CreateProfile();
            var manifest = JsonUtility.FromJson<AllAssetSceneBuilder.SceneManifest>(File.ReadAllText("scratch_out/all-asset-scenes.json"));
            var results = selected == null ? new List<BakeResult>() : JsonUtility.FromJson<BakeReport>(File.ReadAllText(ReportPath)).scenes.ToList();
            foreach (var entry in manifest.scenes)
            {
                if (selected != null && !selected.Contains(entry.path)) continue;
                Debug.Log("ALL_ASSET_BAKE_START " + entry.path);
                var scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);
                var settings = CreateSettings(entry.kit == "BoatEnvironment" ? "Interior" : entry.kit == "World" ? "World" : "Gallery");
                Lightmapping.lightingSettings = settings;
                ConfigurePost(scene, profile);
                VerifyUVs(scene);
                EditorSceneManager.SaveScene(scene);
                var timer = Stopwatch.StartNew();
                Lightmapping.Clear();
                if (!Lightmapping.Bake()) throw new InvalidOperationException("Unity bake failed: " + entry.path);
                timer.Stop();
                if (LightmapSettings.lightmaps.Length == 0 || Lightmapping.lightingDataAsset == null)
                    throw new InvalidOperationException("No generated lighting data: " + entry.path);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                // Reload the saved scene, so verification cannot accidentally validate transient bake state.
                scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);
                var renderers = Components<Renderer>(scene);
                var maps = LightmapSettings.lightmaps;
                int mapped = renderers.Count(r => r.enabled && r.lightmapIndex >= 0 && r.lightmapIndex < maps.Length);
                if (mapped == 0 || Lightmapping.lightingDataAsset == null || maps.Any(m => m.lightmapColor == null))
                    throw new InvalidOperationException("Saved scene has incomplete lightmaps: " + entry.path);
                var lights = Components<Light>(scene).Where(l => l.enabled).ToArray();
                results.RemoveAll(r => r.path == entry.path);
                results.Add(new BakeResult {
                    path = entry.path, kit = entry.kit, prefabCount = entry.prefabCount,
                    lightingData = AssetDatabase.GetAssetPath(Lightmapping.lightingDataAsset), backend = settings.lightmapper.ToString(),
                    lightmapCount = maps.Length, lightmappedRenderers = mapped,
                    bakedLights = lights.Count(l => l.lightmapBakeType == LightmapBakeType.Baked),
                    runtimeLights = lights.Count(l => l.lightmapBakeType != LightmapBakeType.Baked),
                    probeCount = LightmapSettings.lightProbes != null ? LightmapSettings.lightProbes.count : 0,
                    atlasTexels = maps.Sum(m => (long)m.lightmapColor.width * m.lightmapColor.height), seconds = timer.Elapsed.TotalSeconds });
                File.WriteAllText(ReportPath, JsonUtility.ToJson(new BakeReport { prefabCount = manifest.showcasedPrefabCount, sceneCount = results.Count, scenes = results.ToArray() }, true));
                Debug.Log("ALL_ASSET_BAKE_PASS " + entry.path + " maps=" + maps.Length + " renderers=" + mapped + " seconds=" + timer.Elapsed.TotalSeconds.ToString("F1"));
            }
            Debug.Log("ALL_ASSET_BAKE_COMPLETE " + results.Count);
        }
        public static void RepairBoatAndCapture()
        {
            var manifest = JsonUtility.FromJson<AllAssetSceneBuilder.SceneManifest>(File.ReadAllText("scratch_out/all-asset-scenes.json"));
            var affected = manifest.scenes.Where(e => e.kit == "BoatEnvironment" || e.path.EndsWith("BoatEnvironment.prefab.unity")).ToArray();
            AllAssetSceneBuilder.PrepareStaticModelUVs(affected.SelectMany(e => e.prefabPaths).ToArray());
            foreach (var entry in affected)
            {
                var scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);
                if (entry.kit == "BoatEnvironment")
                    foreach (var root in scene.GetRootGameObjects()) AllAssetSceneBuilder.ConfigureRenderers(root, "Environment");
                else AllAssetSceneBuilder.ConfigureRenderers(scene.GetRootGameObjects().First(g => g.name.StartsWith("Gallery_")), entry.kit);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            var paths = affected.Select(e => e.path).ToArray();
            BakeEntries(paths); AllAssetSceneCapture.RunSelected(paths);
            Debug.Log("ALL_ASSET_LIGHTING_WORKFLOW_PASS");
        }
        private static T[] Components<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
        private static LightingSettings CreateSettings(string category)
        {
            string path = SettingsRoot + category + "Lighting.asset";
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(path);
            if (settings == null) { settings = new LightingSettings { name = category + "Lighting" }; AssetDatabase.CreateAsset(settings, path); }
            settings.bakedGI = true; settings.realtimeGI = false;
            settings.lightmapper = LightingSettings.Lightmapper.ProgressiveCPU;
            settings.lightmapResolution = category == "Interior" ? 16 : category == "World" ? 8 : 12;
            settings.lightmapMaxSize = 1024; settings.lightmapPadding = 4; settings.lightmapCompression = LightmapCompression.HighQuality;
            settings.directionalityMode = LightmapsMode.NonDirectional;
            settings.directSampleCount = 32; settings.indirectSampleCount = 128; settings.environmentSampleCount = 64;
            settings.maxBounces = 2; settings.minBounces = 2; settings.ao = true; settings.aoMaxDistance = .35f;
            settings.aoExponentIndirect = 1f; settings.aoExponentDirect = 0f;
            EditorUtility.SetDirty(settings);
            return settings;
        }
        private static void OptimizePipeline()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            if (pipeline == null) throw new InvalidOperationException("PC URP asset missing");
            var serialized = new SerializedObject(pipeline);
            SetInt(serialized, "m_MainLightShadowmapResolution", 1024);
            SetInt(serialized, "m_AdditionalLightsPerObjectLimit", 2);
            SetInt(serialized, "m_ShadowCascadeCount", 2);
            SetInt(serialized, "m_SoftShadowQuality", 1);
            serialized.FindProperty("m_AdditionalLightShadowsSupported").boolValue = false;
            serialized.FindProperty("m_ShadowDistance").floatValue = 35;
            serialized.FindProperty("m_UseSRPBatcher").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }
        private static void SetInt(SerializedObject target, string name, int value)
        {
            var property = target.FindProperty(name);
            if (property == null) throw new InvalidOperationException("URP setting missing: " + name);
            property.intValue = value;
        }
        private static VolumeProfile CreateProfile()
        {
            string path = SettingsRoot + "ShowcasePost.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); profile.name = "ShowcasePost"; AssetDatabase.CreateAsset(profile, path); }
            if (!profile.TryGet<Bloom>(out var bloom)) { bloom = profile.Add<Bloom>(true); AssetDatabase.AddObjectToAsset(bloom, profile); }
            bloom.intensity.Override(.20f); bloom.threshold.Override(1f); bloom.scatter.Override(.55f);
            if (!profile.TryGet<Tonemapping>(out var tonemap)) { tonemap = profile.Add<Tonemapping>(true); AssetDatabase.AddObjectToAsset(tonemap, profile); }
            tonemap.mode.Override(TonemappingMode.Neutral);
            EditorUtility.SetDirty(profile); AssetDatabase.SaveAssets();
            return profile;
        }
        private static void ConfigurePost(Scene scene, VolumeProfile profile)
        {
            var volume = Components<Volume>(scene).FirstOrDefault(v => v.name == "ShowcasePostProcessing");
            if (volume == null) volume = new GameObject("ShowcasePostProcessing").AddComponent<Volume>();
            volume.isGlobal = true; volume.priority = 20; volume.sharedProfile = profile;
            foreach (var camera in Components<Camera>(scene))
            {
                camera.allowHDR = true;
                var additional = camera.GetUniversalAdditionalCameraData();
                additional.renderPostProcessing = true; additional.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            }
        }
        private static void VerifyUVs(Scene scene)
        {
            foreach (var renderer in Components<MeshRenderer>(scene))
            {
                if ((GameObjectUtility.GetStaticEditorFlags(renderer.gameObject) & StaticEditorFlags.ContributeGI) == 0) continue;
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                // Imported UV2 can be checked without making the mesh CPU-readable.
                if (!filter.sharedMesh.HasVertexAttribute(VertexAttribute.TexCoord1))
                    throw new InvalidOperationException("Static mesh needs lightmap UV2: " + renderer.name + " in " + scene.path);
            }
        }
    }
}
