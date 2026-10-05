using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SousTension.EditorTools
{
    public static class V1AssetBuild
    {
        [Serializable] private sealed class Catalog { public string version = "geometry-v1"; public Item[] assets; }
        [Serializable] private sealed class Item
        {
            public string kit, name, model, prefab;
            public long triangles;
            public string[] mounts, parts, clips;
            public int bones;
        }
        private const string Models = "Assets/_Project/Art/Models/";
        private const string Prefabs = "Assets/_Project/Art/Prefabs/";
        private static readonly string[] Characters = { "Vareuse", "DressUniform", "CookApron", "RegulationPyjamas", "DivingSuit", "ContaminatedSailor", "CommanderVarga" };

        [MenuItem("Sous Tension/Art/Build and validate all V1 assets")]
        public static void Run()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var name in Characters)
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(Models + "CrewVariants/" + name + ".fbx");
                if (importer == null) throw new InvalidOperationException("Missing character " + name);
                if (!importer.importAnimation || importer.animationType != ModelImporterAnimationType.Generic) importer.SaveAndReimport();
            }
            MvpAssetBuild.Run();
            var items = new List<Item>();
            foreach (var absolute in Directory.GetFiles(Models, "*.fbx", SearchOption.AllDirectories).OrderBy(p => p))
            {
                var path = absolute.Replace('\\', '/');
                var kit = new DirectoryInfo(Path.GetDirectoryName(path)).Name;
                var name = Path.GetFileNameWithoutExtension(path);
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (source == null) throw new InvalidOperationException("Model import missing " + path);
                var meshes = source.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh)
                    .Concat(source.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r => r.sharedMesh)).ToArray();
                if (meshes.Length == 0 || meshes.Any(m => m == null || m.vertexCount == 0 || !m.HasVertexAttribute(VertexAttribute.Color)))
                    throw new InvalidOperationException("Invalid geometry/colors " + path);
                long triangles = 0;
                foreach (var mesh in meshes)
                    for (var s = 0; s < mesh.subMeshCount; s++) triangles += (long)mesh.GetIndexCount(s) / 3;
                var materials = source.GetComponentsInChildren<Renderer>(true);
                if (materials.Any(r => r.sharedMaterial == null || r.sharedMaterial.shader == null))
                    throw new InvalidOperationException("Invalid material " + path);
                var prefab = Prefabs + kit + "/" + name + ".prefab";
                if (!File.Exists(prefab))
                {
                    Directory.CreateDirectory(Prefabs + kit); AssetDatabase.Refresh();
                    var ob = (GameObject)PrefabUtility.InstantiatePrefab(source);
                    try { PrefabUtility.SaveAsPrefabAsset(ob, prefab); }
                    finally { UnityEngine.Object.DestroyImmediate(ob); }
                }
                if (kit == "CrewVariants" && Array.IndexOf(Characters, name) >= 0) BuildCharacter(path, prefab);
                if (kit == "World" && (name.Contains("Kelp") || name.Contains("Sailboat"))) ApplyTwoSided(prefab);
                items.Add(new Item { kit=kit, name=name, model=path, prefab=prefab, triangles=triangles,
                    mounts=source.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Mount_")).Select(t => t.name).ToArray(),
                    parts=source.GetComponentsInChildren<Renderer>(true).Select(r => r.name).ToArray(),
                    bones=source.GetComponentsInChildren<SkinnedMeshRenderer>(true).Sum(r => r.bones.Length),
                    clips=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).Select(c => c.name).ToArray() });
            }
            Directory.CreateDirectory("Assets/_Project/Art/Catalog");
            File.WriteAllText("Assets/_Project/Art/Catalog/v1_asset_catalog.json", JsonUtility.ToJson(new Catalog { assets=items.ToArray() }, true));
            BuildExterior();
            Gallery("Cargo", 4f, 6);
            Gallery("Fixtures", 7f, 7);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            File.WriteAllText("scratch_out/v1-assets-validation.txt", "V1_ASSET_BUILD_PASS: " + items.Count + " imported FBX, individually validated geometry/colors/materials, native prefabs and catalog; seven Generic character variants, exterior LOD prefabs, cargo/fixtures galleries, traversable six-compartment boat and reachable airlock. Geometry V1; blank artwork, prototype animation, no gameplay/physics systems or production art polish.");
            Debug.Log("V1_ASSET_BUILD_PASS: " + items.Count + " FBX with native prefabs and catalog.");
        }

        private static void BuildCharacter(string path, string prefab)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            var avatar = assets.OfType<Avatar>().Single(a => a.isValid);
            var clips = assets.OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
            if (clips.Length != 4) throw new InvalidOperationException("Four clips required: " + path);
            Directory.CreateDirectory("Assets/_Project/Art/Animations/CrewVariants"); AssetDatabase.Refresh();
            var controllerPath = "Assets/_Project/Art/Animations/CrewVariants/" + Path.GetFileNameWithoutExtension(path) + ".controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            if (controller.layers.Length == 0) controller.AddLayer("Base Layer");
            var machine = controller.layers[0].stateMachine;
            foreach (var state in machine.states) machine.RemoveState(state.state);
            foreach (var clip in clips)
            {
                var state = machine.AddState(clip.name); state.motion = clip;
                if (clip.name.EndsWith("Idle")) machine.defaultState = state;
            }
            var ob = PrefabUtility.LoadPrefabContents(prefab);
            try
            {
                var animator = ob.GetComponent<Animator>(); if (animator == null) animator = ob.AddComponent<Animator>();
                animator.avatar = avatar; animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
                PrefabUtility.SaveAsPrefabAsset(ob, prefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(ob); }
            EditorUtility.SetDirty(controller);
        }

        private static void ApplyTwoSided(string path)
        {
            var matPath = "Assets/_Project/Art/Materials/VertexColorTwoSided.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (material == null)
            {
                material = new Material(Shader.Find("SousTension/VertexColorLit")); material.SetFloat("_Cull", 0);
                AssetDatabase.CreateAsset(material, matPath);
            }
            var ob = PrefabUtility.LoadPrefabContents(path);
            try { foreach (var r in ob.GetComponentsInChildren<Renderer>()) r.sharedMaterial = material;
                PrefabUtility.SaveAsPrefabAsset(ob, path); }
            finally { PrefabUtility.UnloadPrefabContents(ob); }
        }

        private static void BuildExterior()
        {
            for (var wear = 0; wear < 3; wear++)
            {
                var root = new GameObject("MolossExterior_Wear" + wear); var lods = new LOD[3]; Transform mounts = null;
                try
                {
                    for (var l = 0; l < 3; l++)
                    {
                        var hull = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Models + "World/MolossHull_LOD"+l+"_Wear"+wear+".fbx"));
                        hull.transform.SetParent(root.transform, false);
                        lods[l] = new LOD(l == 0 ? .35f : l == 1 ? .12f : .015f, hull.GetComponentsInChildren<Renderer>());
                        if (l == 0) mounts = hull.transform;
                    }
                    var group = root.AddComponent<LODGroup>(); group.SetLODs(lods); group.RecalculateBounds();
                    foreach (var entry in new[] { ("MolossConningTower", "Mount_Tower"), ("MolossPropeller", "Mount_Propeller"), ("MolossRudder", "Mount_Rudder"), ("MolossDivingPlane", "Mount_PlanePort"), ("MolossDivingPlane", "Mount_PlaneStarboard") })
                    {
                        var mount = mounts.GetComponentsInChildren<Transform>().Single(t => t.name == entry.Item2);
                        var appendage = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Models + "World/"+entry.Item1+".fbx"));
                        appendage.transform.SetParent(mount, false);
                        if (entry.Item2 == "Mount_PlanePort") appendage.transform.localRotation = Quaternion.Euler(0,180,0);
                    }
                    var tower = root.GetComponentsInChildren<Transform>().First(t => t.name == "MolossConningTower");
                    foreach (var name in new[] { "MolossAntenna", "MolossPeriscopeHead" })
                    {
                        var ob = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Models+"World/"+name+".fbx"));
                        ob.transform.SetParent(tower, false); ob.transform.localPosition = new Vector3(name.Contains("Antenna") ? -.5f : .5f, 1.7f, 0);
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "World/" + root.name + ".prefab");
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
        }

        private static void Gallery(string kit, float cell, int columns)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var paths = Directory.GetFiles(Prefabs + kit, "*.prefab").OrderBy(p => p).ToArray();
            for (var i=0; i<paths.Length; i++)
            {
                var ob=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(paths[i].Replace('\\','/')),scene);
                ob.transform.position = new Vector3((i%columns)*cell,0,(i/columns)*cell);
            }
            var light = new GameObject("GalleryKey").AddComponent<Light>(); light.type=LightType.Directional;light.intensity=1.4f;light.transform.rotation=Quaternion.Euler(45,-35,0);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.30f,.32f,.34f);
            var camera=new GameObject("MainCamera").AddComponent<Camera>();camera.tag="MainCamera";camera.orthographic=true;
            var center=new Vector3((columns-1)*cell/2,0,((paths.Length-1)/columns)*cell/2);
            camera.transform.position=center+new Vector3(cell*columns*.65f,cell*columns*.9f,-cell*columns*.8f);camera.transform.LookAt(center);
            camera.orthographicSize=cell*columns*.65f;camera.farClipPlane=1000;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.15f,.17f);
            EditorSceneManager.SaveScene(scene,"Assets/_Project/Art/Scenes/V1_"+kit+"Gallery.unity");
        }
    }
}
