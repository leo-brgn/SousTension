using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SousTension.EditorTools
{
    public static class CrewAssetValidation
    {
        public static void Run()
        {
            Validate("SailorBase", new[] { "Idle", "Walk", "ValveTurn", "Faint" }, 27);
            Validate("FirstPersonArms", new[] { "HandsIdle", "HandsGrip" }, 17);
            Debug.Log("CREW_ASSET_VALIDATION_PASS: rigged sailor and first-person arms");
        }

        private static void Require(bool valid, string message)
        {
            if (!valid) throw new InvalidOperationException(message);
        }

        private static void Validate(string name, string[] takes, int minimumBones)
        {
            string path = "Assets/_Project/Art/Models/Crew/" + name + ".fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            Require(importer != null && importer.importAnimation, name + ": animation import");
            Require(importer.animationType == ModelImporterAnimationType.Generic, name + ": Generic rig");
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            var avatar = assets.OfType<Avatar>().FirstOrDefault();
            Require(avatar != null && avatar.isValid, name + ": valid avatar");
            var clips = assets.OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
            Require(clips.Length == takes.Length, name + ": expected " + takes.Length + " clips, found " + clips.Length);
            foreach (string take in takes)
                Require(clips.Any(c => c.name.Contains(take) && c.length > 0), name + ": missing take " + take);
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Require(renderers.Length == 1, name + ": one skinned renderer");
            foreach (var renderer in renderers)
            {
                var mesh = renderer.sharedMesh;
                Require(mesh != null && mesh.vertexCount > 0, name + ": skin mesh");
                Require(renderer.bones.Length >= minimumBones - 1 && renderer.bones.All(b => b != null), name + ": bones bound");
                Require(mesh.bindposes.Length == renderer.bones.Length, name + ": bind poses");
                Require(mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Color), name + ": vertex colors");
                Require(renderer.sharedMaterial != null && renderer.sharedMaterial.shader != null, name + ": material");
                var bounds = renderer.localBounds;
                Require(bounds.size.y > .2f && bounds.size.y < 3f && bounds.size.x > .2f && bounds.size.x < 3f, name + ": metre dimensions " + bounds.size);
                Debug.Log("CREW_VALIDATED " + name + ": vertices=" + mesh.vertexCount + ", bones=" + renderer.bones.Length + ", dimensions=" + bounds.size + ", clips=" + string.Join(", ", clips.Select(c => c.name)));
            }
        }
    }
}
