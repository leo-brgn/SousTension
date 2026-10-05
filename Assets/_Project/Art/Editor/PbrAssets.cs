using System.IO;
using UnityEditor;
using UnityEngine;

namespace SousTension.EditorTools
{
    /// <summary>
    /// Pipeline des modèles PBR (blender/finish.py) : Assets/_Project/Art/PBR/{Models,Textures,Materials}/&lt;Kit&gt;/&lt;Nom&gt;.*
    /// - réglages d'import des textures (BaseColor sRGB, Mask linéaire, Normal en carte de normales) et des modèles (UV, tangentes, pas de couleur de sommet) ;
    /// - un matériau par modèle (shader SousTension/PBRPacked + ses trois textures), créé par BuildMaterials().
    /// </summary>
    public static class PbrAssets
    {
        public const string Root = "Assets/_Project/Art/PBR";
        public const string ModelsRoot = Root + "/Models/";
        public const string TexturesRoot = Root + "/Textures/";
        public const string MaterialsRoot = Root + "/Materials/";

        public static string MaterialPathFor(string modelAssetPath)
        {
            string kit = Path.GetFileName(Path.GetDirectoryName(modelAssetPath));
            string name = Path.GetFileNameWithoutExtension(modelAssetPath);
            return MaterialsRoot + kit + "/" + name + ".mat";
        }

        [MenuItem("SousTension/Art/Build PBR Materials")]
        public static void BuildMaterials()
        {
            var shader = Shader.Find("SousTension/PBRPacked");
            if (shader == null) { Debug.LogError("[PBR] shader SousTension/PBRPacked introuvable"); return; }
            if (!Directory.Exists(ModelsRoot)) { Debug.LogWarning("[PBR] aucun modèle dans " + ModelsRoot); return; }
            int created = 0, missing = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var fbx in Directory.GetFiles(ModelsRoot, "*.fbx", SearchOption.AllDirectories))
                {
                    string path = fbx.Replace('\\', '/');
                    string kit = Path.GetFileName(Path.GetDirectoryName(path));
                    string name = Path.GetFileNameWithoutExtension(path);
                    string tex = TexturesRoot + kit + "/" + name;
                    if (!File.Exists(tex + "_BaseColor.png")) { missing++; continue; }
                    string matPath = MaterialPathFor(path);
                    Directory.CreateDirectory(Path.GetDirectoryName(matPath));
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    if (mat == null)
                    {
                        mat = new Material(shader) { name = name };
                        AssetDatabase.CreateAsset(mat, matPath);
                        created++;
                    }
                    mat.shader = shader;
                    mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "_BaseColor.png"));
                    mat.SetTexture("_MaskMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "_Mask.png"));
                    mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "_Normal.png"));
                    // lueur : seulement si le modèle a des faces émissives ; intensité HDR (3x : voyants nets sous bloom)
                    var emission = File.Exists(tex + "_Emission.png") ? AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "_Emission.png") : null;
                    mat.SetTexture("_EmissionMap", emission);
                    mat.SetColor("_EmissionColor", emission != null ? new Color(3f, 3f, 3f, 1f) : Color.black);
                    EditorUtility.SetDirty(mat);
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.SaveAssets();
            Debug.Log("[PBR] matériaux créés : " + created + " ; modèles sans textures : " + missing);
            // réimport des modèles pour qu'ils prennent leur matériau
            foreach (var fbx in Directory.GetFiles(ModelsRoot, "*.fbx", SearchOption.AllDirectories))
                AssetDatabase.ImportAsset(fbx.Replace('\\', '/'), ImportAssetOptions.ForceUpdate);
        }
    }

    public sealed class PbrTexturePostprocessor : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(PbrAssets.TexturesRoot)) return;
            var t = (TextureImporter)assetImporter;
            string file = Path.GetFileNameWithoutExtension(assetPath);
            t.mipmapEnabled = true;
            t.wrapMode = TextureWrapMode.Clamp;           // atlas UV avec marge : pas de répétition
            t.anisoLevel = 4;
            t.maxTextureSize = 1024;
            t.textureCompression = TextureImporterCompression.Compressed;
            if (file.EndsWith("_Emission")) { t.textureType = TextureImporterType.Default; t.sRGBTexture = true; t.alphaSource = TextureImporterAlphaSource.None; }
            else if (file.EndsWith("_Normal")) { t.textureType = TextureImporterType.NormalMap; }
            else if (file.EndsWith("_Mask")) { t.textureType = TextureImporterType.Default; t.sRGBTexture = false; t.alphaSource = TextureImporterAlphaSource.FromInput; t.alphaIsTransparency = false; }
            else { t.textureType = TextureImporterType.Default; t.sRGBTexture = true; t.alphaSource = TextureImporterAlphaSource.None; }
        }
    }

    public sealed class PbrModelPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(PbrAssets.ModelsRoot)) return;
            var m = (ModelImporter)assetImporter;
            m.globalScale = 1f;
            m.useFileScale = true;
            m.importAnimation = false;
            m.importCameras = false;
            m.importLights = false;
            m.importBlendShapes = false;
            m.materialImportMode = ModelImporterMaterialImportMode.None;
            m.importNormals = ModelImporterNormals.Import;
            m.importTangents = ModelImporterTangents.CalculateMikk;
            m.meshCompression = ModelImporterMeshCompression.Off;
            m.isReadable = false;
            m.preserveHierarchy = true;
            m.generateSecondaryUV = false;
        }

        private void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.StartsWith(PbrAssets.ModelsRoot)) return;
            var mat = AssetDatabase.LoadAssetAtPath<Material>(PbrAssets.MaterialPathFor(assetPath));
            if (mat == null) return;                              // avant BuildMaterials() : pas encore de matériau
            foreach (var r in root.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = mat;
        }
    }
}
