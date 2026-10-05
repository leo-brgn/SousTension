using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SousTension.EditorTools
{
    public static class MachinesAssetValidation
    {
        [Serializable] private sealed class Manifest { public Entry[] assets; }
        [Serializable] private sealed class Entry { public string name; public int triangles; public string[] parts; public string[] mounts; }

        public static void Run()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            const string folder = "Assets/_Project/Art/Models/Machines/";
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(folder + "machines_manifest.json"));
            if (manifest.assets.Length != 6) throw new InvalidOperationException("Incomplete machine-room kit");
            foreach (var entry in manifest.assets)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(folder + entry.name + ".fbx");
                if (asset == null) throw new InvalidOperationException("Missing model: " + entry.name);
                var meshes = asset.GetComponentsInChildren<MeshFilter>(true);
                if (meshes.Length != entry.parts.Length) throw new InvalidOperationException("Part count: " + entry.name);
                var transforms = asset.GetComponentsInChildren<Transform>(true);
                var names = new HashSet<string>(transforms.Select(t => t.name));
                foreach (var name in entry.parts.Concat(entry.mounts))
                    if (!names.Contains(name)) throw new InvalidOperationException("Missing stable name: " + entry.name + "/" + name);
                var triangles = 0;
                var bounds = new Bounds();
                var initialized = false;
                foreach (var mesh in meshes)
                {
                    if (!mesh.sharedMesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Color))
                        throw new InvalidOperationException("Missing vertex colors: " + entry.name);
                    for (var i = 0; i < mesh.sharedMesh.subMeshCount; i++)
                        triangles += (int)mesh.sharedMesh.GetIndexCount(i) / 3;
                    var renderer = mesh.GetComponent<MeshRenderer>();
                    if (renderer.sharedMaterial == null || renderer.sharedMaterial.shader.name != "SousTension/VertexColorLit")
                        throw new InvalidOperationException("Material: " + entry.name);
                    if (!initialized) { bounds = renderer.bounds; initialized = true; }
                    else bounds.Encapsulate(renderer.bounds);
                }
                if (triangles != entry.triangles) throw new InvalidOperationException("Lost geometry: " + entry.name + " imported=" + triangles + " expected=" + entry.triangles);
                if (bounds.size.x < 0.07f || bounds.size.x > 2.6f || bounds.size.y > 2f)
                    throw new InvalidOperationException("Metre scale: " + entry.name + " " + bounds.size);
                // Check animated parts have distinct pivots, rather than all being reset to the root.
                foreach (var mesh in meshes.Where(m => m.name != "Body"))
                    if (mesh.transform.localPosition.sqrMagnitude < 0.000001f)
                        throw new InvalidOperationException("Lost animation pivot: " + entry.name + "/" + mesh.name);
                if (entry.name == "ElectricalPanel")
                {
                    var sockets = transforms.Where(t => t.name.StartsWith("Mount_Breaker")).ToArray();
                    if (sockets.Length != 20 || sockets.Select(t => t.localPosition).Distinct().Count() != 20)
                        throw new InvalidOperationException("Electrical panel needs twenty distinct sockets");
                }
                Debug.Log("MACHINES VALIDATED " + entry.name + " triangles=" + triangles + " bounds=" + bounds.size);
            }
            Directory.CreateDirectory("scratch_out");
            File.WriteAllText("scratch_out/machines-unity-validation.txt", "Six FBX models validated: geometry, vertex colors, materials, part and socket names, animation pivots, metre scale and twenty breaker sockets.");
        }
    }
}
