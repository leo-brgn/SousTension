using UnityEditor;
using UnityEngine;

namespace SousTension.EditorTools
{
    // Runs after the shared static-art importer to preserve crew skinning and clips.
    public sealed class CrewModelPostprocessor : AssetPostprocessor
    {
        public override int GetPostprocessOrder() => 100;

        private void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.StartsWith("Assets/_Project/Art/Models/Crew/")) return;
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/CrewSurface.mat");
            if (material == null) return;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterial = material;
        }

        private void OnPreprocessModel()
        {
            var variantName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            var animatedVariant = assetPath.StartsWith("Assets/_Project/Art/Models/CrewVariants/")
                && System.Array.IndexOf(new[] { "Vareuse", "DressUniform", "CookApron", "RegulationPyjamas", "DivingSuit", "ContaminatedSailor", "CommanderVarga" }, variantName) >= 0;
            if (!assetPath.StartsWith("Assets/_Project/Art/Models/Crew/") && !animatedVariant) return;
            var importer = (ModelImporter)assetImporter;
            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.optimizeGameObjects = false;
            importer.skinWeights = ModelImporterSkinWeights.Standard;
        }
    }
}
