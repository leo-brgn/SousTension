using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SousTension.EditorTools
{
    public static class ToolsDamageAssetValidation
    {
        [Serializable] private sealed class Manifest { public Entry[] assets; }
        [Serializable] private sealed class Entry { public string name; public int triangles; public string[] parts; public string[] mounts; public float prototype_mass_kg; }
        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        public static void Run()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            int count = 0;
            foreach (var kit in new[] { "Tools", "Damage" })
            {
                string folder = "Assets/_Project/Art/Models/" + kit + "/";
                var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(folder + kit.ToLowerInvariant() + "_manifest.json"));
                if (manifest?.assets == null || manifest.assets.Length == 0) throw new InvalidOperationException("Missing manifest " + kit);
                foreach (var entry in manifest.assets)
                {
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(folder + entry.name + ".fbx");
                    if (model == null) throw new InvalidOperationException("Missing " + entry.name);
                    var meshes = model.GetComponentsInChildren<MeshFilter>(true);
                    var names = new HashSet<string>(model.GetComponentsInChildren<Transform>(true).Select(t => t.name));
                    if (meshes.Length != entry.parts.Length) throw new InvalidOperationException("Part count " + entry.name);
                    foreach (var name in entry.parts.Concat(entry.mounts))
                        if (!names.Contains(name)) throw new InvalidOperationException("Missing name " + entry.name + "/" + name);
                    int tris = 0;
                    foreach (var filter in meshes)
                    {
                        var mesh = filter.sharedMesh;
                        if (mesh == null || mesh.vertexCount == 0 || !mesh.HasVertexAttribute(VertexAttribute.Color) || !mesh.HasVertexAttribute(VertexAttribute.TexCoord0))
                            throw new InvalidOperationException("Mesh/colors/UV " + entry.name);
                        for (int s = 0; s < mesh.subMeshCount; s++) tris += (int)mesh.GetIndexCount(s) / 3;
                        var b = mesh.bounds;
                        if (!Finite(b.center.x) || !Finite(b.center.y) || !Finite(b.center.z) || !Finite(b.size.x) || !Finite(b.size.y) || !Finite(b.size.z) || b.size.sqrMagnitude == 0)
                            throw new InvalidOperationException("Invalid bounds " + entry.name);
                        using (var data = Mesh.AcquireReadOnlyMeshData(mesh))
                        using (var vertices = new Unity.Collections.NativeArray<Vector3>(mesh.vertexCount, Unity.Collections.Allocator.Temp))
                        {
                            data[0].GetVertices(vertices);
                            foreach (var v in vertices)
                                if (!Finite(v.x) || !Finite(v.y) || !Finite(v.z)) throw new InvalidOperationException("Nonfinite vertex " + entry.name);
                        }
                    }
                    if (tris != entry.triangles) throw new InvalidOperationException("Triangle mismatch " + entry.name + ":" + tris + " != " + entry.triangles);
                    if (kit == "Tools")
                    {
                        if (!Finite(entry.prototype_mass_kg) || entry.prototype_mass_kg <= 0) throw new InvalidOperationException("Invalid prototype mass " + entry.name);
                        CreatePortable(model, entry);
                    }
                    count++;
                    Debug.Log("TOOLS DAMAGE VALIDATED " + entry.name + " triangles=" + tris);
                }
            }
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("scratch_out");
            File.WriteAllText("scratch_out/tools-damage-unity-validation.txt", count + " models checked for geometry counts, colors, UVs, stable parts/mounts, finite vertices and bounds. Tools prefabs generated with positive prototype masses and per-part box colliders. No localized paper content or runtime VFX behavior verified.");
        }
        private static void CreatePortable(GameObject model, Entry entry)
        {
            string folder = "Assets/_Project/Art/Prefabs/Tools";
            Directory.CreateDirectory(folder);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                instance.name = entry.name;
                var body = instance.AddComponent<Rigidbody>();
                body.mass = entry.prototype_mass_kg;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                foreach (var filter in instance.GetComponentsInChildren<MeshFilter>(true))
                {
                    var collider = filter.gameObject.AddComponent<BoxCollider>();
                    collider.center = filter.sharedMesh.bounds.center;
                    var size = filter.sharedMesh.bounds.size;
                    collider.size = new Vector3(Mathf.Max(.006f,size.x),Mathf.Max(.006f,size.y),Mathf.Max(.006f,size.z));
                }
                var prefab = PrefabUtility.SaveAsPrefabAsset(instance, folder + "/" + entry.name + ".prefab");
                if (prefab == null) throw new InvalidOperationException("Prefab save " + entry.name);
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
    }
}
