using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SousTension.EditorTools
{
    public static class DetailAssetValidation
    {
        public static void Run()
        {
            foreach (var kit in new[] { "Radio", "Living", "Signage", "Torpedoes", "Cargo", "Fixtures", "World", "CrewVariants" })
            {
                var folder = "Assets/_Project/Art/Models/" + kit;
                var paths = Directory.GetFiles(folder, "*.fbx");
                if (paths.Length == 0) throw new InvalidOperationException("No detail assets in " + kit);
                var target = "Assets/_Project/Art/Prefabs/" + kit;
                Directory.CreateDirectory(target);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                foreach (var path in paths)
                {
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\', '/'));
                    if (source == null) throw new InvalidOperationException("Missing import " + path);
                    var renderers = source.GetComponentsInChildren<MeshRenderer>(true);
                    if (renderers.Length == 0 && source.GetComponentsInChildren<SkinnedMeshRenderer>().Length == 0)
                        throw new InvalidOperationException("Empty asset " + path);
                    foreach (var renderer in renderers)
                    {
                        var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                        if (mesh == null || mesh.vertexCount == 0 || !mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Color)
                            || renderer.sharedMaterial == null || renderer.sharedMaterial.shader == null)
                            throw new InvalidOperationException("Mesh/material invalid " + path);
                        if (mesh.bounds.size.magnitude > (kit == "World" ? 2000 : 12)) throw new InvalidOperationException("Invalid metre scale " + path);
                    }
                    foreach (var renderer in source.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        if (renderer.sharedMesh == null || renderer.bones.Any(b => b == null)
                            || renderer.sharedMesh.bindposes.Length != renderer.bones.Length || renderer.sharedMaterial == null)
                            throw new InvalidOperationException("Invalid variant skin " + path);
                        var imported = AssetDatabase.LoadAllAssetsAtPath(path.Replace('\\', '/'));
                        if (!imported.OfType<Avatar>().Any(a => a.isValid) || imported.OfType<AnimationClip>().Count(c => !c.name.StartsWith("__preview__")) != 4)
                            throw new InvalidOperationException("Invalid variant avatar or four clips " + path);
                    }
                    var ob = (GameObject)PrefabUtility.InstantiatePrefab(source);
                    try
                    {
                        // Wall plates are decoration; equipment keeps independent part colliders.
                        if (kit != "Signage")
                            foreach (var filter in ob.GetComponentsInChildren<MeshFilter>(true))
                                filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                        PrefabUtility.SaveAsPrefabAsset(ob, target + "/" + source.name + ".prefab");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(ob); }
                }
                Debug.Log("DETAIL_ASSETS_VALIDATED " + kit + ": " + paths.Length + " models and prefabs");
            }
        }
    }
}
