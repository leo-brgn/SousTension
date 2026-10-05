using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SousTension.EditorTools
{
    /// <summary>Validate the primary circuit after importing the Blender FBX exports.</summary>
    public static class PrimaryAssetValidation
    {
        public static void Run()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var name in new[] { "PrimaryPump", "PrimaryPump_Broken", "PrimaryValve" })
            {
                var path = "Assets/_Project/Art/Models/Primary/" + name + ".fbx";
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null) throw new InvalidOperationException("Missing model: " + path);
                var expected = name == "PrimaryValve" ? 3 : 4;
                var meshes = asset.GetComponentsInChildren<MeshFilter>(true);
                if (meshes.Length != expected) throw new InvalidOperationException("Unexpected parts: " + name);
                var bounds = new Bounds();
                var initialized = false;
                foreach (var renderer in asset.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (renderer.sharedMaterial == null || renderer.sharedMaterial.shader.name != "SousTension/VertexColorLit")
                        throw new InvalidOperationException("Missing vertex color material: " + name);
                    if (!initialized) { bounds = renderer.bounds; initialized = true; }
                    else bounds.Encapsulate(renderer.bounds);
                }
                // These props are about one metre wide; catches centimetre/metre export regressions.
                if (bounds.size.x < 0.4f || bounds.size.x > 1.5f)
                    throw new InvalidOperationException("Incorrect metre scale: " + name + " " + bounds.size);
                var mount = name == "PrimaryValve" ? "Mount_A" : "Mount_Inlet";
                var found = false;
                foreach (var transform in asset.GetComponentsInChildren<Transform>(true))
                    if (transform.name == mount) found = true;
                if (!found) throw new InvalidOperationException("Missing pipe mount: " + name);
                Debug.Log("PRIMARY VALIDATED " + name + " meshes=" + meshes.Length + " bounds=" + bounds.size);
            }
            Directory.CreateDirectory("scratch_out");
            File.WriteAllText("scratch_out/primary-unity-validation.txt", "Three primary circuit FBX assets imported successfully: parts, metre scale, materials and pipe mounts verified.");
        }
    }
}
