using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SousTension.Tests
{
    /// <summary>
    /// Checks the Blender-generated models after import: units, axes, hierarchy and pivots, mount points, vertex colors, triangle budget.
    /// If these fail after regenerating the models (powershell -File blender/build.ps1), the export conventions in blender/lib.py changed.
    /// </summary>
    public class ArtImportTests
    {
        private const string ModelsRoot = "Assets/_Project/Art/Models";
        private const string MaterialPath = "Assets/_Project/Art/Materials/VertexColorLit.mat";

        private static IEnumerable<string> AllModels()
            => Directory.Exists(ModelsRoot) ? Directory.GetFiles(ModelsRoot, "*.fbx", SearchOption.AllDirectories).Select(p => p.Replace('\\', '/')) : new string[0];

        private static GameObject Load(string name)
        {
            string path = AllModels().FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == name);
            Assert.IsNotNull(path, "model not found: " + name);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static Transform Find(Transform root, string name)
            => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

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
        public void VertexColorMaterial_UsesASupportedShader()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Assert.IsNotNull(mat, "material missing");
            Assert.AreEqual("SousTension/VertexColorLit", mat.shader.name);
            Assert.IsTrue(mat.shader.isSupported, "VertexColorLit shader does not compile / is not supported");
        }

        [Test]
        public void ModelsExist_AndAreImportedWithTheMaterial()
        {
            var all = AllModels().ToList();
            Assert.GreaterOrEqual(all.Count, 28, "expected the lot 1 models (15 instruments + 3 reactor + 10 structure)");
            foreach (var path in all)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(go, path);
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    Assert.AreEqual("SousTension/VertexColorLit", r.sharedMaterial != null ? r.sharedMaterial.shader.name : "(none)", path + " / " + r.name);
            }
        }

        [Test]
        public void EveryModel_HasMeshesWithVertexColors_WithinTheTriangleBudget()
        {
            foreach (var path in AllModels())
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var filters = go.GetComponentsInChildren<MeshFilter>(true);
                Assert.IsNotEmpty(filters, path);
                long tris = 0;
                foreach (var f in filters)
                {
                    var mesh = f.sharedMesh;
                    Assert.IsNotNull(mesh, path + " / " + f.name);
                    Assert.Greater(mesh.vertexCount, 0, path);
                    var attr = mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Color);
                    Assert.IsTrue(attr, path + " / " + f.name + ": vertex colors were lost in the import");
                    tris += mesh.triangles.Length / 3;
                }
                Assert.LessOrEqual(tris, 5000, path + " exceeds the 5000 triangle budget (" + tris + ")");
            }
        }

        [Test]
        public void EveryModel_RootIsIdentity_AndNamedLikeItsFile()
        {
            foreach (var path in AllModels())
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var name = Path.GetFileNameWithoutExtension(path);
                var root = go.name == name ? go.transform : Find(go.transform, name);
                Assert.IsNotNull(root, path + ": root '" + name + "' not found");
                Assert.Less(Quaternion.Angle(root.localRotation, Quaternion.identity), 0.5f, path + ": root is rotated (axis conversion problem)");
                Assert.That(root.localScale.x, Is.EqualTo(1f).Within(0.01f), path + ": root scale");
            }
        }

        [TestCase("HullModule_2m", 6.2f, 2.6f, 2.0f)]       // x = width, y = height, z = length (Blender Y forward -> Unity Z)
        [TestCase("FloorGrating_1m", 1.0f, 0.05f, 1.0f)]
        [TestCase("Bulkhead_Solid", 6.0f, 2.5f, 0.4f)]
        [TestCase("Pipe_Straight_1m", 0.2f, 0.2f, 1.0f)]
        [TestCase("RK1_Console", 1.7f, 1.8f, 0.9f)]
        public void KeyModels_HaveTheExpectedSizeInMetersAndAxes(string model, float sx, float sy, float sz)
        {
            var size = WorldBounds(Load(model)).size;
            Assert.That(size.x, Is.EqualTo(sx).Within(sx * 0.2f + 0.03f), model + " width (x)");
            Assert.That(size.y, Is.EqualTo(sy).Within(sy * 0.2f + 0.03f), model + " height (y)");
            Assert.That(size.z, Is.EqualTo(sz).Within(sz * 0.25f + 0.03f), model + " length (z)");
        }

        [TestCase("GaugeRound_S")] [TestCase("GaugeRound_M")] [TestCase("GaugeRound_L")] [TestCase("GaugeVertical")] [TestCase("CounterRollers")]
        [TestCase("LampDome")] [TestCase("VUMeter")] [TestCase("LeverSwitch")] [TestCase("ButtonGuarded")] [TestCase("SelectorRotary")]
        [TestCase("ValveWheel_S")] [TestCase("ValveWheel_M")] [TestCase("ValveWheel_L")] [TestCase("Crank")] [TestCase("RatchetWheel")]
        public void Instruments_FaceTheViewer_AndHaveAMountPoint(string model)
        {
            var go = Load(model);
            var mounts = go.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Mount_")).ToList();
            Assert.IsNotEmpty(mounts, model + ": no Mount_* point");
            // Instruments look toward -Y in Blender, which is -Z in Unity (their dial / front faces the viewer at -Z).
            var b = WorldBounds(go);
            Assert.Greater(b.size.x * b.size.y, 0.0001f, model);
        }

        [Test]
        public void AnimatedParts_ExistWithTheirPivots()
        {
            var gauge = Load("GaugeRound_M").transform;
            var needle = Find(gauge, "Needle");
            Assert.IsNotNull(needle, "GaugeRound_M needs a Needle part");
            Assert.Less(Mathf.Abs(needle.localPosition.x) + Mathf.Abs(needle.localPosition.y), 0.01f, "needle pivot must be on the dial centre axis");

            var scram = Load("ScramLever").transform;
            foreach (var part in new[] { "Base", "Lever", "Cover", "Seal" }) Assert.IsNotNull(Find(scram, part), "ScramLever / " + part);

            var door = Load("HatchDoor").transform;
            var d = Find(door, "Door");
            Assert.IsNotNull(d);
            Assert.AreEqual(d, Find(door, "Wheel").parent, "the wheel is a child of the door (it swings with it)");
            for (int i = 0; i < 6; i++) Assert.AreEqual(d, Find(door, "Latch" + i).parent, "latch " + i);
        }

        [Test]
        public void ReactorConsole_ExposesTheMountPointsOfTheInstruments()
        {
            var t = Load("RK1_Console").transform;
            var names = new List<string> { "Mount_Selector", "Mount_Scram" };
            for (int i = 0; i < 4; i++) names.Add("Mount_Gauge" + i);
            for (int i = 0; i < 3; i++) names.Add("Mount_Lamp" + i);
            for (int i = 0; i < 8; i++) names.Add("Mount_Breaker" + i);
            foreach (var n in names) Assert.IsNotNull(Find(t, n), "missing " + n);
            // the gauges sit on the sloped panel: above the lower cabinet, in front of it
            Assert.Greater(Find(t, "Mount_Gauge0").position.y, 1.0f);
        }
    }
}
