using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SousTension.EditorTools
{
    public static class CentralAssetValidation
    {
        [Serializable] private sealed class Manifest { public Entry[] assets; }
        [Serializable] private sealed class Entry
        {
            public string name; public int triangles; public string[] parts;
            public Point[] mounts, pivots; public float massKg;
        }
        [Serializable] private sealed class Point { public string name; public float[] positionBlender; }
        private const string Folder = "Assets/_Project/Art/Models/Central/";
        private static void Require(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
        private static Vector3 Convert(float[] p) => new Vector3(-p[0], p[2], -p[1]);

        [MenuItem("Sous Tension/Art/Validate central assets")]
        public static void Run()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Folder + "central_manifest.json"));
            Require(manifest != null && manifest.assets.Length == 12, "Central: expected twelve models");
            foreach (var entry in manifest.assets)
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + entry.name + ".fbx");
                Require(root != null, "Central missing " + entry.name);
                var all = root.GetComponentsInChildren<Transform>(true);
                var meshes = root.GetComponentsInChildren<MeshFilter>(true);
                Require(meshes.Length == entry.parts.Length, entry.name + ": part count");
                foreach (var part in entry.parts) Require(meshes.Count(m => m.name == part) == 1, entry.name + ": stable part " + part);
                foreach (var point in entry.mounts.Concat(entry.pivots))
                {
                    var matches = all.Where(t => t.name == point.name).ToArray();
                    Require(matches.Length == 1, entry.name + ": stable pivot/socket " + point.name);
                    Require(Vector3.Distance(matches[0].localPosition, Convert(point.positionBlender)) < .0002f,
                        entry.name + ": pivot conversion " + point.name + " imported=" + matches[0].localPosition + " expected=" + Convert(point.positionBlender));
                }
                var triangles = 0; var bounds = new Bounds(); var initialized = false;
                foreach (var filter in meshes)
                {
                    var mesh = filter.sharedMesh;
                    Require(mesh != null && mesh.vertexCount > 0, entry.name + ": mesh");
                    Require(mesh.HasVertexAttribute(VertexAttribute.Color), entry.name + ": colors");
                    for (int i = 0; i < mesh.subMeshCount; i++) triangles += (int)mesh.GetIndexCount(i) / 3;
                    var renderer = filter.GetComponent<MeshRenderer>();
                    Require(renderer != null && renderer.sharedMaterial != null && renderer.sharedMaterial.shader.name == "SousTension/VertexColorLit", entry.name + ": material");
                    if (!initialized) { bounds = renderer.bounds; initialized = true; } else bounds.Encapsulate(renderer.bounds);
                    bool paper = entry.name == "ChartPaperBlank" || entry.name == "ManualOK114LoosePage" || filter.name.StartsWith("TurnableLeaf") || filter.name.Contains("PageBlock");
                    if (paper) Require(mesh.HasVertexAttribute(VertexAttribute.TexCoord0), entry.name + "/" + filter.name + ": UV0");
                }
                Require(triangles == entry.triangles, entry.name + ": triangle count " + triangles + " expected " + entry.triangles);
                // Per-model width catches scale loss for tiny portable and large fixed assets alike.
                float expected = Width(entry.name);
                Require(Mathf.Abs(bounds.size.x - expected) < expected * .08f + .002f, entry.name + ": metre width " + bounds.size.x + " expected " + expected);
                if (entry.name == "ManualOK114OpenBinder")
                {
                    Require(meshes.Count(m => m.name.StartsWith("TurnableLeaf")) == 30, "Manual: thirty hinged leaves");
                    Require(Mathf.Abs(entry.massKg - 8f) < .001f, "Manual: GDD eight kilogram mass");
                }
                Debug.Log("CENTRAL VALIDATED " + entry.name + " triangles=" + triangles + " dimensions=" + bounds.size);
            }
            Require(Directory.GetFiles(Folder + "ManualArtworkTemplates", "*_TEMPLATE.png").Length == 30, "Manual: thirty blank art templates");
            Directory.CreateDirectory("scratch_out");
            File.WriteAllText("scratch_out/central-unity-validation.txt", "Twelve FBX validated: geometry, vertex colors, shader, stable names, exact pivots/socket conversion, metre scale, thirty hinged manual leaves and paper UV0. Thirty blank spread art templates exist. Final procedures and gameplay are not validated.");
            Debug.Log("CENTRAL_ASSET_VALIDATION_PASS");
        }

        private static float Width(string name)
        {
            switch (name)
            {
                case "CentralSteeringWheel": return .656f;
                case "CentralDepthTrimConsole": return 1.2f;
                case "EngineTelegraphFivePosition": return .51f;
                case "CentralChartTable": return 1.55f;
                case "ChartPaperBlank": return .99f;
                case "GreasePencil": return .017f;
                case "ManualOK114OpenBinder": return .656f;
                case "CentralInstrumentConsole": return 1.68f;
                case "PneumaticArrivalStation": return .43f;
                case "PneumaticCapsule": return .142f;
                case "RolledOrderPaper": return .048f;
                case "ManualOK114LoosePage": return .276f;
                default: throw new InvalidOperationException(name);
            }
        }

        [MenuItem("Sous Tension/Art/Build central assembly prefabs")]
        public static void BuildPrefabs()
        {
            const string prefabs = "Assets/_Project/Art/Prefabs/Central/";
            Directory.CreateDirectory(prefabs);
            var chart = new GameObject("CentralChartStation");
            try
            {
                var table = Instance("CentralChartTable", chart.transform);
                Attach("ChartPaperBlank", table, "Mount_ChartPaper");
                Attach("ManualOK114OpenBinder", table, "Mount_Manual");
                var pencil = Attach("GreasePencil", table, "Mount_Pencil");
                pencil.transform.localRotation = Quaternion.Euler(90, 0, 15);
                PrefabUtility.SaveAsPrefabAsset(chart, prefabs + "CentralChartStation.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(chart); }
            var console = new GameObject("CentralMultiConsole");
            try
            {
                var body = Instance("CentralInstrumentConsole", console.transform);
                foreach (var socket in body.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Mount_Gauge")).ToArray())
                {
                    var gaugeAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Models/Instruments/GaugeRound_M.fbx");
                    Require(gaugeAsset != null, "Missing GaugeRound_M");
                    var gauge = (GameObject)PrefabUtility.InstantiatePrefab(gaugeAsset);
                    gauge.transform.SetParent(socket, false);
                }
                PrefabUtility.SaveAsPrefabAsset(console, prefabs + "CentralMultiConsole.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(console); }
            AssetDatabase.SaveAssets();
        }
        private static GameObject Instance(string name, Transform parent)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".fbx");
            Require(source != null, "Missing central model " + name);
            var ob = (GameObject)PrefabUtility.InstantiatePrefab(source); ob.transform.SetParent(parent, false); return ob;
        }
        private static GameObject Attach(string name, GameObject parent, string socket)
        {
            var mount = parent.GetComponentsInChildren<Transform>().Single(t => t.name == socket);
            return Instance(name, mount);
        }
    }
}
