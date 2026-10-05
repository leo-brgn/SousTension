using UnityEditor;
using UnityEngine;

namespace SousTension.EditorTools
{
    /// <summary>
    /// Import settings for the Blender models (Assets/_Project/Art/Models). Models come from blender/*.py: vertex colors, no texture,
    /// no animation, hierarchy kept (root + animatable parts with their pivots + Mount_* empties used to place instruments).
    /// All renderers get the shared vertex-color material so nothing renders pink.
    /// </summary>
    public sealed class ArtModelPostprocessor : AssetPostprocessor
    {
        private const string ModelsRoot = "Assets/_Project/Art/Models/";
        private const string MaterialPath = "Assets/_Project/Art/Materials/VertexColorLit.mat";

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ModelsRoot)) return;
            var m = (ModelImporter)assetImporter;
            m.globalScale = 1f;
            m.useFileScale = true;
            m.importAnimation = false;
            m.importCameras = false;
            m.importLights = false;
            m.importBlendShapes = false;
            m.materialImportMode = ModelImporterMaterialImportMode.None;
            m.importNormals = ModelImporterNormals.Import;
            m.meshCompression = ModelImporterMeshCompression.Off;
            m.isReadable = false;
            m.preserveHierarchy = true;
            // Preserve importer UV2 requests made by the baked showcase builder.
        }

        private void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.StartsWith(ModelsRoot)) return;
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null) return;                      // first import before the material exists: reimport once
            foreach (var r in root.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = material;
        }
    }
}
