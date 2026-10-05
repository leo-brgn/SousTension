using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SousTension.Tests
{
    /// <summary>
    /// Vérifie les modèles PBR produits par blender/finish.py après import : UV dans l'atlas, tangentes, matériau à trois textures,
    /// réglages des textures, et fidélité au modèle d'origine (mêmes dimensions, mêmes parties, mêmes pivots).
    /// </summary>
    public class PbrImportTests
    {
        // Chemins du pipeline PBR (copie de PbrAssets : les assemblies de tests ne voient pas l'assembly Editor prédéfinie)
        private const string PbrModelsRoot = "Assets/_Project/Art/PBR/Models/";
        private const string PbrTexturesRoot = "Assets/_Project/Art/PBR/Textures/";

        private static IEnumerable<string> PbrModels()
            => Directory.Exists(PbrModelsRoot)
                ? Directory.GetFiles(PbrModelsRoot, "*.fbx", SearchOption.AllDirectories).Select(p => p.Replace('\\', '/')).OrderBy(p => p)
                : Enumerable.Empty<string>();

        private static string OriginalPath(string pbrPath)
        {
            string kit = Path.GetFileName(Path.GetDirectoryName(pbrPath));
            return "Assets/_Project/Art/Models/" + kit + "/" + Path.GetFileName(pbrPath);
        }

        private static Bounds WorldBounds(GameObject asset)
        {
            var go = Object.Instantiate(asset);
            try
            {
                var rs = go.GetComponentsInChildren<Renderer>();
                var b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                return b;
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void PbrShader_IsSupported()
        {
            var shader = Shader.Find("SousTension/PBRPacked");
            Assert.IsNotNull(shader, "shader SousTension/PBRPacked introuvable");
            Assert.IsTrue(shader.isSupported, "le shader PBRPacked ne compile pas");
        }

        [Test]
        public void ModelsExist()
        {
            Assert.GreaterOrEqual(PbrModels().Count(), 1, "aucun modèle PBR : lancer blender/finish_all.ps1 puis blender/sync_pbr.ps1");
        }

        [Test]
        public void EveryModel_HasAtlasUVs_Tangents_AndATexturedMaterial()
        {
            var problems = new List<string>();
            foreach (var path in PbrModels())
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) { problems.Add(path + " : non chargé"); continue; }
                var meshes = go.GetComponentsInChildren<MeshFilter>(true).Select(f => (f.name, f.sharedMesh))
                    .Concat(go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(s => (s.name, s.sharedMesh))).ToList();
                if (meshes.Count == 0) { problems.Add(path + " : aucun mesh"); continue; }
                foreach (var (meshName, mesh) in meshes)
                {
                    if (mesh.uv.Length != mesh.vertexCount) { problems.Add(path + " / " + meshName + " : pas d'UV"); continue; }
                    foreach (var uv in mesh.uv)
                        if (uv.x < -0.01f || uv.x > 1.01f || uv.y < -0.01f || uv.y > 1.01f) { problems.Add(path + " / " + meshName + " : UV hors de l'atlas"); break; }
                    if (mesh.tangents.Length != mesh.vertexCount) problems.Add(path + " / " + meshName + " : pas de tangentes");
                }
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    var m = r.sharedMaterial;
                    if (m == null || m.shader.name != "SousTension/PBRPacked") { problems.Add(path + " / " + r.name + " : matériau PBR manquant"); continue; }
                    foreach (var prop in new[] { "_BaseMap", "_MaskMap", "_BumpMap" })
                        if (m.GetTexture(prop) == null) problems.Add(path + " : texture " + prop + " manquante");
                }
            }
            Assert.IsEmpty(problems, string.Join("\n", problems.Take(25)) + (problems.Count > 25 ? "\n… " + (problems.Count - 25) + " autres" : ""));
        }

        [Test]
        public void EmissiveModels_HaveTheirEmissionMapOnTheMaterial()
        {
            var problems = new List<string>();
            int emissive = 0;
            foreach (var png in Directory.GetFiles(PbrTexturesRoot, "*_Emission.png", SearchOption.AllDirectories).Select(p => p.Replace('\\', '/')))
            {
                emissive++;
                string kit = Path.GetFileName(Path.GetDirectoryName(png));
                string name = Path.GetFileName(png).Replace("_Emission.png", "");
                var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/PBR/Materials/" + kit + "/" + name + ".mat");
                if (mat == null || mat.GetTexture("_EmissionMap") == null) problems.Add(name + " : carte d'émission non branchée");
                else if (mat.GetColor("_EmissionColor").maxColorComponent < 1f) problems.Add(name + " : intensité d'émission nulle");
            }
            Assert.Greater(emissive, 0, "aucune carte d'émission : relancer blender/finish_all.ps1 puis blender/sync_pbr.ps1");
            Assert.IsEmpty(problems, string.Join("\n", problems.Take(25)));
        }

        [Test]
        public void Textures_AreImportedWithTheRightColorSpaceAndType()
        {
            var problems = new List<string>();
            foreach (var png in Directory.GetFiles(PbrTexturesRoot, "*.png", SearchOption.AllDirectories).Select(p => p.Replace('\\', '/')))
            {
                var t = (TextureImporter)AssetImporter.GetAtPath(png);
                if (png.EndsWith("_Normal.png") && t.textureType != TextureImporterType.NormalMap) problems.Add(png + " : doit être une carte de normales");
                if (png.EndsWith("_Mask.png") && t.sRGBTexture) problems.Add(png + " : le masque doit être linéaire (sRGB désactivé)");
                if (png.EndsWith("_Emission.png") && !t.sRGBTexture) problems.Add(png + " : la lueur doit être sRGB");
                if (png.EndsWith("_BaseColor.png") && !t.sRGBTexture) problems.Add(png + " : la couleur doit être sRGB");
            }
            Assert.IsEmpty(problems, string.Join("\n", problems.Take(25)));
        }

        [Test]
        public void EveryModel_KeepsTheSizeHierarchyAndPivotsOfTheOriginal()
        {
            var problems = new List<string>();
            int compared = 0;
            foreach (var path in PbrModels())
            {
                var original = AssetDatabase.LoadAssetAtPath<GameObject>(OriginalPath(path));
                if (original == null) continue;                              // l'original a été renommé ou supprimé : ignoré
                var pbr = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var bo = WorldBounds(original); var bp = WorldBounds(pbr);
                if ((bo.size - bp.size).magnitude > 0.005f + bo.size.magnitude * 0.01f) problems.Add(path + " : dimensions " + bp.size + " != " + bo.size);
                if ((bo.center - bp.center).magnitude > 0.005f + bo.size.magnitude * 0.01f) problems.Add(path + " : position " + bp.center + " != " + bo.center);
                var no = original.GetComponentsInChildren<Transform>(true).Select(t => t.name).OrderBy(n => n).ToList();
                var np = pbr.GetComponentsInChildren<Transform>(true).Select(t => t.name).OrderBy(n => n).ToList();
                if (!no.SequenceEqual(np)) problems.Add(path + " : parties différentes");
                foreach (var t in original.GetComponentsInChildren<Transform>(true))
                {
                    var tp = pbr.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == t.name);
                    if (tp != null && (tp.position - t.position).magnitude > 0.003f) { problems.Add(path + " / " + t.name + " : pivot déplacé"); break; }
                }
                compared++;
            }
            Assert.IsEmpty(problems, string.Join("\n", problems.Take(25)) + (problems.Count > 25 ? "\n… " + (problems.Count - 25) + " autres" : ""));
            Assert.Greater(compared, 0, "aucun modèle comparable");
        }
    }
}
