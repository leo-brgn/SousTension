using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SousTension.EditorTools
{
    /// <summary>Complete prefab inventory galleries. Baking and capture are performed by the caller.</summary>
    public static class AllAssetSceneBuilder
    {
        [Serializable] public sealed class SceneManifest { public SceneEntry[] scenes; public int discoveredPrefabCount; public int showcasedPrefabCount; }
        [Serializable] public sealed class SceneEntry { public string path, kit; public int prefabCount; public string[] prefabPaths; }
        private const string PrefabRoot = "Assets/_Project/Art/Prefabs/";
        private const string SceneRoot = "Assets/_Project/Art/Scenes/Showcase/";
        private const string MaterialRoot = "Assets/_Project/Art/Materials/Showcase/";
        private static Material floorMaterial, warmEmission, amberEmission, redEmission, emergencyOff;

        [MenuItem("Sous Tension/Art/Build all asset showcase scenes")]
        public static void Build()
        {
            Directory.CreateDirectory(SceneRoot);
            Directory.CreateDirectory(MaterialRoot);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            InitializeMaterials();
            var paths = Directory.GetFiles(PrefabRoot, "*.prefab", SearchOption.AllDirectories)
                .Select(p => p.Replace('\\', '/')).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            if (paths.Length == 0) throw new InvalidOperationException("No art prefabs discovered.");
            PrepareStaticModelUVs(paths);
            var entries = new List<SceneEntry>();
            foreach (var group in paths.GroupBy(p => p.Substring(PrefabRoot.Length).Split('/')[0]))
            {
                var grouped = group.ToArray();
                int limit = group.Key == "World" ? 12 : 24;
                for (int start = 0; start < grouped.Length; start += limit)
                {
                    var batch = grouped.Skip(start).Take(limit).ToArray();
                    string suffix = grouped.Length > limit ? "_" + (start / limit + 1).ToString("D2") : "";
                    string scenePath = SceneRoot + group.Key + suffix + ".unity";
                    BuildGallery(group.Key, batch, scenePath);
                    entries.Add(new SceneEntry { path = scenePath, kit = group.Key, prefabCount = batch.Length, prefabPaths = batch });
                }
            }
            BuildBoatCopy(entries);
            int represented = entries.Where(e => e.kit != "BoatEnvironment").Sum(e => e.prefabCount);
            if (represented != paths.Length || entries.Where(e => e.kit != "BoatEnvironment").SelectMany(e => e.prefabPaths).Distinct().Count() != paths.Length)
                throw new InvalidOperationException("Showcase prefab coverage differs from discovered inventory.");
            Directory.CreateDirectory("scratch_out");
            File.WriteAllText("scratch_out/all-asset-scenes.json", JsonUtility.ToJson(new SceneManifest { scenes = entries.ToArray(), discoveredPrefabCount = paths.Length, showcasedPrefabCount = represented }, true));
            AssetDatabase.SaveAssets();
            Debug.Log("ALL ASSET SCENES BUILT " + entries.Count + " scenes; complete prefab coverage=" + represented);
        }
        private static void InitializeMaterials()
        {
            floorMaterial = Material("WhiteMatte", new Color(.80f,.81f,.78f), Color.black);
            warmEmission = Material("WarmLens", new Color(1,.94f,.79f), new Color(1,.81f,.52f) * 5f);
            amberEmission = Material("AmberIndicator", new Color(.95f,.64f,.20f), new Color(1,.55f,.13f) * 1.8f);
            redEmission = Material("EmergencyLens", new Color(.75f,.05f,.025f), new Color(1,.045f,.01f) * 1.2f);
            emergencyOff = Material("EmergencyLensOff", new Color(.20f,.025f,.015f), Color.black);
        }
        private static Material Material(string name, Color baseColor, Color emission)
        {
            string path = MaterialRoot + name + ".mat";
            var result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (result == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("URP Lit shader unavailable.");
                result = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(result, path);
            }
            result.SetColor("_BaseColor", baseColor);
            result.SetFloat("_Smoothness", .18f);
            result.SetFloat("_Metallic", 0);
            result.SetColor("_EmissionColor", emission);
            if (emission.maxColorComponent > 0)
            {
                result.EnableKeyword("_EMISSION");
                result.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            }
            else { result.DisableKeyword("_EMISSION"); result.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack; }
            EditorUtility.SetDirty(result);
            return result;
        }
        private static bool DynamicVisual(string kit, GameObject instance)
        {
            string name = instance.name.ToLowerInvariant();
            return kit == "Effects" || instance.GetComponentInChildren<ParticleSystem>(true) != null ||
                EffectName(name);
        }
        private static bool EffectName(string name) => name.Contains("bilgewater") || name.Contains("steamcard") ||
            name.Contains("decal") || name.Contains("glowcard") || name.Contains("puddle");
        public static void PrepareStaticModelUVs(string[] prefabPaths)
        {
            var modelPaths = new HashSet<string>();
            foreach (var path in prefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) throw new InvalidOperationException("Prefab load " + path);
                string kit = path.Substring(PrefabRoot.Length).Split('/')[0];
                if (DynamicVisual(kit, prefab) || prefab.GetComponentInChildren<SkinnedMeshRenderer>(true) != null) continue;
                foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    string meshPath = AssetDatabase.GetAssetPath(filter.sharedMesh);
                    if (meshPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) modelPaths.Add(meshPath);
                }
            }
            foreach (string path in modelPaths)
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null || importer.generateSecondaryUV) continue;
                importer.generateSecondaryUV = true;
                importer.secondaryUVPackMargin = 8;
                importer.SaveAndReimport();
                // Shared import postprocessors can override defaults. Persist an explicit override and detect failure.
                importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer != null && !importer.generateSecondaryUV)
                {
                    importer.generateSecondaryUV = true;
                    EditorUtility.SetDirty(importer);
                    AssetDatabase.WriteImportSettingsIfDirty(path);
                    Debug.LogWarning("Static UV setting overridden by postprocessor for " + path + "; scene bake must verify UV2.");
                }
            }
        }
        private static void BuildGallery(string kit, string[] paths, string scenePath)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var gallery = new GameObject("Gallery_" + kit).transform;
            float rowX = 0, rowZ = 0, rowDepth = 0;
            int rowCount = 0;
            int columns = kit == "World" ? 3 : 4;
            var combined = new Bounds(); bool first = true;
            var probePositions = new List<Vector3>();
            foreach (string path in paths)
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
                instance.name = Path.GetFileNameWithoutExtension(path);
                instance.transform.SetParent(gallery, false);
                foreach (var body in instance.GetComponentsInChildren<Rigidbody>(true)) body.isKinematic = true;
                foreach (var light in instance.GetComponentsInChildren<Light>(true)) { light.lightmapBakeType = LightmapBakeType.Baked; light.enabled = false; light.shadows = LightShadows.None; }
                var bounds = RenderBounds(instance);
                float spacing = Mathf.Max(.45f, Mathf.Max(bounds.size.x,bounds.size.z) * .075f);
                if (rowCount >= columns) { rowZ += rowDepth + spacing; rowX = 0; rowDepth = 0; rowCount = 0; }
                float width = Mathf.Max(bounds.size.x,.20f), depth = Mathf.Max(bounds.size.z,.20f);
                instance.transform.position += new Vector3(rowX - bounds.min.x, .035f - bounds.min.y, rowZ - bounds.min.z);
                bounds = RenderBounds(instance);
                ConfigureRenderers(instance, kit);
                ApplyEmission(instance, false);
                if (first) { combined = bounds; first = false; } else combined.Encapsulate(bounds);
                if (instance.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
                {
                    var c = bounds.center;
                    probePositions.Add(c + new Vector3(-.6f,.2f,-.6f)); probePositions.Add(c + new Vector3(.6f,.2f,-.6f));
                    probePositions.Add(c + new Vector3(-.6f,.8f,.6f)); probePositions.Add(c + new Vector3(.6f,.8f,.6f));
                }
                rowX += width + spacing; rowDepth = Mathf.Max(rowDepth,depth); rowCount++;
            }
            Floor(combined);
            AddDirectional();
            SetAmbient();
            AddProbes(probePositions);
            FitCamera(combined);
            EditorSceneManager.SaveScene(scene, scenePath);
        }
        private static Bounds RenderBounds(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer) && r.enabled).ToArray();
            if (renderers.Length == 0) return new Bounds(instance.transform.position, Vector3.one);
            var b = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) b.Encapsulate(renderer.bounds);
            if (b.size.sqrMagnitude < .00001f) b = new Bounds(instance.transform.position, Vector3.one);
            return b;
        }
        public static void ConfigureRenderers(GameObject instance, string kit)
        {
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                bool dynamic = kit == "Effects";
                for (var ancestor = renderer.transform; ancestor != null; ancestor = ancestor.parent)
                {
                    string n = ancestor.name.ToLowerInvariant();
                    dynamic |= EffectName(n);
                    if (ancestor == instance.transform) break;
                }
                if (renderer is SkinnedMeshRenderer || renderer is ParticleSystemRenderer || dynamic)
                {
                    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, 0);
                    renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                    if (renderer is MeshRenderer dynamicMesh) dynamicMesh.receiveGI = ReceiveGI.LightProbes;
                    renderer.lightmapIndex = -1;
                    continue;
                }
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic);
                var meshRenderer = renderer as MeshRenderer;
                if (meshRenderer == null) continue;
                meshRenderer.receiveGI = ReceiveGI.Lightmaps;
                float size = renderer.bounds.size.magnitude;
                meshRenderer.scaleInLightmap = kit == "World" ? Mathf.Clamp(2f / Mathf.Max(size,1), .03f,.15f) : size < .25f ? 2f : .65f;
            }
        }
        private static void ApplyEmission(GameObject instance, bool boat)
        {
            foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                string name = renderer.name.ToLowerInvariant();
                string parents = renderer.transform.parent != null ? renderer.transform.parent.name.ToLowerInvariant() : "";
                bool emergency = parents.Contains("emergency") || instance.name.ToLowerInvariant().Contains("emergency");
                for (var ancestor=renderer.transform.parent;ancestor!=null;ancestor=ancestor.parent)
                    emergency |= ancestor.name.ToLowerInvariant().Contains("emergency");
                bool lens = name == "lens" || name.Contains("lightlens") || name.Contains("lampglass");
                bool indicator = name.Contains("display") || name.Contains("indicatorlens") || name.Contains("coilglow");
                if (lens) renderer.sharedMaterial = emergency ? (boat ? emergencyOff : redEmission) : warmEmission;
                else if (indicator) renderer.sharedMaterial = amberEmission;
            }
        }
        private static void Floor(Bounds b)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "StaticWhiteGalleryFloor";
            floor.transform.position = new Vector3(b.center.x,-.10f,b.center.z);
            floor.transform.localScale = new Vector3(Mathf.Max(2,b.size.x+2),.20f,Mathf.Max(2,b.size.z+2));
            floor.GetComponent<MeshRenderer>().sharedMaterial = floorMaterial;
            floor.GetComponent<MeshRenderer>().scaleInLightmap = .04f;
            GameObjectUtility.SetStaticEditorFlags(floor, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic);
        }
        private static void AddDirectional()
        {
            var light = new GameObject("BakedGalleryKey").AddComponent<Light>();
            light.type = LightType.Directional; light.lightmapBakeType = LightmapBakeType.Baked;
            light.color = new Color(1,.94f,.83f); light.intensity = 1.4f; light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(48,-35,0);
            AddBakedFill();
        }
        public static void AddBakedFill()
        {
            var light = new GameObject("BakedGalleryFill").AddComponent<Light>();
            light.type = LightType.Directional; light.lightmapBakeType = LightmapBakeType.Baked;
            light.color = new Color(.90f,.95f,1f); light.intensity = 1.2f;
            light.shadows = LightShadows.Soft; light.transform.rotation = Quaternion.Euler(40,145,0);
        }
        private static void SetAmbient()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.18f,.20f,.23f);
            RenderSettings.ambientIntensity = .55f;
            RenderSettings.skybox = null;
        }
        private static void AddProbes(List<Vector3> positions)
        {
            if (positions.Count < 4) return;
            var probes = new GameObject("ShowcaseLightProbes").AddComponent<LightProbeGroup>();
            probes.probePositions = positions.ToArray();
        }
        private static void FitCamera(Bounds bounds)
        {
            var camera = new GameObject("MainCamera").AddComponent<Camera>();
            ReframeCamera(camera, bounds);
        }
        public static void ReframeCamera(Camera camera, Bounds bounds)
        {
            camera.tag = "MainCamera"; camera.orthographic = true; camera.aspect = 1900f/1100f;
            camera.backgroundColor = new Color(.14f,.18f,.21f); camera.clearFlags = CameraClearFlags.SolidColor;
            var target = bounds.center;
            var direction = new Vector3(.65f,.65f,.9f).normalized;
            float size = Mathf.Max(bounds.size.magnitude,2);
            camera.transform.position = target + direction * (size*2+10);
            camera.transform.LookAt(target);
            float minX=float.MaxValue,maxX=float.MinValue,minY=float.MaxValue,maxY=float.MinValue;
            for (int i=0;i<8;i++)
            {
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                var point = camera.transform.InverseTransformPoint(corner);
                minX=Mathf.Min(minX,point.x);maxX=Mathf.Max(maxX,point.x);minY=Mathf.Min(minY,point.y);maxY=Mathf.Max(maxY,point.y);
            }
            camera.orthographicSize = Mathf.Max((maxY-minY)*.5f,(maxX-minX)*.5f/camera.aspect)*1.08f;
            camera.nearClipPlane = .03f; camera.farClipPlane = size*6+100;
        }
        private static void BuildBoatCopy(List<SceneEntry> entries)
        {
            const string original = "Assets/_Project/Art/Scenes/BoatEnvironment.unity";
            if (!File.Exists(original)) throw new InvalidOperationException("Boat source scene missing.");
            var scene = EditorSceneManager.OpenScene(original,OpenSceneMode.Single);
            var probes = new List<Vector3>();
            foreach (var root in scene.GetRootGameObjects())
            {
                ConfigureRenderers(root,"Environment"); ApplyEmission(root,true);
                foreach (var light in root.GetComponentsInChildren<Light>(true))
                {
                    bool emergency=false;
                    for(var ancestor=light.transform;ancestor!=null;ancestor=ancestor.parent)
                        emergency |= ancestor.name.ToLowerInvariant().Contains("emergency");
                    if (emergency) { light.enabled=false; continue; }
                    light.lightmapBakeType=LightmapBakeType.Baked;light.shadows=LightShadows.Soft;
                    if (light.type != LightType.Directional) { light.enabled=true;light.range=6.5f;light.intensity=18f;light.color=new Color(1,.88f,.66f); }
                    else light.enabled=false;
                }
            }
            // Interior six-room probe lattice, source scene is 28 metres along Unity Z.
            for(float z=-13;z<=13;z+=2)
                foreach(float x in new[]{-2f,0f,2f}) foreach(float y in new[]{.6f,1.8f}) probes.Add(new Vector3(x,y,z));
            AddProbes(probes); SetAmbient();
            string path=SceneRoot+"BoatEnvironment_Lit.unity";
            EditorSceneManager.SaveScene(scene,path);
            entries.Add(new SceneEntry{path=path,kit="BoatEnvironment",prefabCount=0,prefabPaths=Array.Empty<string>()});
        }
    }
}
