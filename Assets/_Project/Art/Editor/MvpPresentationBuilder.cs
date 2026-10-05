using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SousTension.EditorTools
{
    /// <summary>Author prototype visual prefabs; no simulation or network behaviour.</summary>
    public static class MvpPresentationBuilder
    {
        public static void Build()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Directory.CreateDirectory("Assets/_Project/Art/Materials/Effects");
            Directory.CreateDirectory("Assets/_Project/Art/Prefabs/Effects");
            Directory.CreateDirectory("Assets/_Project/Art/Prefabs/Lighting");
            Directory.CreateDirectory("Assets/_Project/Art/Prefabs/Crew");
            Directory.CreateDirectory("Assets/_Project/Art/Animations");
            AssetDatabase.Refresh();
            var shader = Shader.Find("SousTension/PrototypeEffect");
            if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Effect shader failed compilation");
            var water = EffectMaterial("BilgeWater", shader, 0, new Color(0.19f, 0.33f, 0.34f, 0.70f));
            EffectMaterial("Droplet", shader, 5, new Color(0.40f, 0.66f, 0.69f, 0.65f));
            EffectMaterial("ContactSpark", shader, 5, new Color(1f, 0.64f, 0.12f, 0.9f));
            var steam = EffectMaterial("Steam", shader, 1, new Color(0.82f, 0.84f, 0.81f, 0.80f));
            var frost = EffectMaterial("Frost", shader, 2, new Color(0.77f, 0.88f, 0.88f, 0.85f));
            var condensation = EffectMaterial("Condensation", shader, 3, new Color(0.62f, 0.76f, 0.75f, 0.65f));
            var contamination = EffectMaterial("Contamination", shader, 4, new Color(0.35f, 0.48f, 0.22f, 0.70f));
            var glow = EffectMaterial("Cherenkov", shader, 5, new Color(0.19f, 0.70f, 1f, 0.70f));
            foreach (var name in new[] { "BilgeWaterTile_2m", "BilgeWaterCompartment_4x6", "BilgeWaterCompartment_6x8",
                "SteamCard", "FrostDecal", "CondensationDecal", "ContaminationPuddle", "CherenkovGlowCard" })
            {
                var ob = Instance("Damage", name);
                var material = name.StartsWith("Bilge") ? water : name == "SteamCard" ? steam : name == "FrostDecal" ? frost
                    : name == "CondensationDecal" ? condensation : name == "ContaminationPuddle" ? contamination : glow;
                foreach (var renderer in ob.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = material;
                Save(ob, "Effects/" + name);
            }
            for (var i = 0; i < 3; i++) Leak(new[] { "Small", "Medium", "Large" }[i], new[] { 0.025f, 0.055f, 0.09f }[i], water);
            Sparks();
            for (var state = 0; state < 4; state++) Lighting(state);
            Crew("SailorBase", "Idle");
            Crew("FirstPersonArms", "HandsIdle");
            PortableCentral();
            FlashlightLight();
            AssetDatabase.SaveAssets();
            Debug.Log("MVP PRESENTATION VALIDATED: effect materials, eight water/decal prefabs, three leaks, sparks, four lighting states, two crew Animator prefabs.");
        }

        private static Material EffectMaterial(string name, Shader shader, int mode, Color color)
        {
            var path = "Assets/_Project/Art/Materials/Effects/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            mat.SetColor("_Tint", color);
            mat.SetFloat("_Mode", mode);
            mat.SetFloat("_Strength", 0.8f);
            mat.SetFloat("_WaveAmplitude", mode == 0 ? 0.012f : 0);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static GameObject Instance(string kit, string name)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Models/" + kit + "/" + name + ".fbx");
            if (model == null) throw new InvalidOperationException("Missing presentation model " + name);
            return (GameObject)PrefabUtility.InstantiatePrefab(model);
        }

        private static void Save(GameObject ob, string name)
        {
            PrefabUtility.SaveAsPrefabAsset(ob, "Assets/_Project/Art/Prefabs/" + name + ".prefab");
            UnityEngine.Object.DestroyImmediate(ob);
        }

        private static ParticleSystem Particles(Transform parent, string name)
        {
            var ob = new GameObject(name);
            ob.transform.SetParent(parent, false);
            var ps = ob.AddComponent<ParticleSystem>();
            ps.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return ps;
        }

        private static void Leak(string size, float radius, Material water)
        {
            var ob = Instance("Damage", "LeakAnchor_" + size);
            var mount = ob.GetComponentsInChildren<Transform>().First(t => t.name == "Mount_Jet");
            var ps = Particles(mount, "WaterJet");
            var main = ps.main; main.duration = 2; main.loop = true; main.startLifetime = 0.55f;
            main.startSpeed = 3.5f; main.startSize = radius; main.startColor = new Color(0.65f, 0.85f, 0.87f, 0.7f);
            main.gravityModifier = 0.5f; main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = ps.emission; emission.rateOverTime = 60;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 8; shape.radius = radius;
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Effects/Droplet.mat");
            Save(ob, "Effects/Leak_" + size);
        }

        private static void Sparks()
        {
            var ob = Instance("Damage", "SparkAnchor");
            var mount = ob.GetComponentsInChildren<Transform>().First(t => t.name == "Mount_Sparks");
            var ps = Particles(mount, "ContactSparks");
            var main = ps.main; main.duration = 2; main.startLifetime = 0.25f; main.startSpeed = 2;
            main.startSize = 0.02f; main.startColor = new Color(1, 0.72f, 0.25f); main.gravityModifier = 0.7f;
            var emission = ps.emission; emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)12) });
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 40; shape.radius = 0.015f;
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Effects/ContactSpark.mat");
            Save(ob, "Effects/ContactSparks");
        }

        private static void Lighting(int state)
        {
            var names = new[] { "Normal", "LowVoltage", "Emergency", "Blackout" };
            var color = state == 1 ? new Color(1, 0.49f, 0.10f) : state == 2 ? new Color(0.85f, 0.03f, 0.015f) : new Color(1, 0.91f, 0.76f);
            var ob = Instance("Lighting", state == 2 ? "EmergencyLamp" : "CompartmentCeilingLight");
            var mount = ob.GetComponentsInChildren<Transform>().First(t => t.name == "Mount_Light");
            var light = new GameObject("LampLight").AddComponent<Light>();
            light.transform.SetParent(mount, false);
            light.type = LightType.Point; light.range = 6; light.intensity = state == 0 ? 2.4f : 0.9f;
            light.color = color; light.enabled = state != 3; light.shadows = LightShadows.Soft;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit missing");
            var path = "Assets/_Project/Art/Materials/Effects/Lamp_" + names[state] + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            mat.SetColor("_BaseColor", color); mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", state == 3 ? Color.black : color * 2);
            foreach (var renderer in ob.GetComponentsInChildren<Renderer>())
                if (renderer.name == "Lens") renderer.sharedMaterial = mat;
            Save(ob, "Lighting/" + names[state]);
        }

        private static void Crew(string name, string initial)
        {
            var ob = Instance("Crew", name);
            var animator = ob.GetComponent<Animator>();
            if (animator == null) animator = ob.AddComponent<Animator>();
            var path = "Assets/_Project/Art/Animations/" + name + ".controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            if (controller.layers.Length == 0) controller.AddLayer("Base Layer");
            var machine = controller.layers[0].stateMachine;
            foreach (var state in machine.states) machine.RemoveState(state.state);
            var clips = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Models/Crew/" + name + ".fbx")
                .OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
            foreach (var clip in clips)
            {
                var state = machine.AddState(clip.name);
                state.motion = clip;
                if (clip.name.EndsWith(initial)) machine.defaultState = state;
            }
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            EditorUtility.SetDirty(controller);
            Save(ob, "Crew/" + name);
        }

        private static void PortableCentral()
        {
            Directory.CreateDirectory("Assets/_Project/Art/Prefabs/Central");
            var names = new[] { "ManualOK114OpenBinder", "GreasePencil", "PneumaticCapsule", "RolledOrderPaper", "ManualOK114LoosePage" };
            var masses = new[] { 8f, 0.035f, 0.45f, 0.008f, 0.025f };
            for (var i = 0; i < names.Length; i++)
            {
                var ob = Instance("Central", names[i]);
                if (names[i] == "ManualOK114OpenBinder") ApplyManualMaterials(ob);
                var body = ob.AddComponent<Rigidbody>();
                body.mass = masses[i]; body.interpolation = RigidbodyInterpolation.Interpolate;
                foreach (var filter in ob.GetComponentsInChildren<MeshFilter>())
                {
                    var collider = filter.gameObject.AddComponent<BoxCollider>();
                    collider.center = filter.sharedMesh.bounds.center;
                    var size = filter.sharedMesh.bounds.size;
                    collider.size = new Vector3(Mathf.Max(0.005f, size.x), Mathf.Max(0.005f, size.y), Mathf.Max(0.005f, size.z));
                }
                Save(ob, "Central/" + names[i]);
            }
            Directory.CreateDirectory("Assets/_Project/Art/Prefabs/Cargo");
            for (var i = 0; i < 3; i++)
            {
                var name = "CargoCrate_" + new[] { "S", "M", "L" }[i];
                var ob = Instance("Environment", name);
                var body = ob.AddComponent<Rigidbody>(); body.mass = new[] { 3f, 12f, 28f }[i];
                var mesh = ob.GetComponentInChildren<MeshFilter>();
                var collider = mesh.gameObject.AddComponent<BoxCollider>();
                collider.center = mesh.sharedMesh.bounds.center; collider.size = mesh.sharedMesh.bounds.size;
                Save(ob, "Cargo/" + name);
            }
        }

        private static void FlashlightLight()
        {
            const string path = "Assets/_Project/Art/Prefabs/Tools/Flashlight.prefab";
            var ob = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var mount = ob.GetComponentsInChildren<Transform>().First(t => t.name == "Mount_Light");
                // The prop's longitudinal Blender Z axis imports as Unity Y.
                var beam = mount.Find("Beam");
                if (beam == null) beam = new GameObject("Beam").transform;
                beam.SetParent(mount, false); beam.localRotation = Quaternion.Euler(-90, 0, 0);
                var spot = beam.GetComponent<Light>();
                if (spot == null) spot = beam.gameObject.AddComponent<Light>();
                spot.type = LightType.Spot; spot.range = 12; spot.spotAngle = 48; spot.intensity = 4;
                spot.color = new Color(1, 0.93f, 0.80f); spot.shadows = LightShadows.Soft;
                PrefabUtility.SaveAsPrefabAsset(ob, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(ob); }
        }

        public static void ApplyManualMaterials(GameObject ob)
        {
            Directory.CreateDirectory("Assets/_Project/Art/Materials/Manual");
            AssetDatabase.Refresh();
            foreach (var renderer in ob.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!renderer.name.StartsWith("TurnableLeaf")) continue;
                var index = int.Parse(renderer.name.Substring("TurnableLeaf".Length));
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Models/Central/ManualArtworkTemplates/OK114_LayoutSpread"
                    + index.ToString("00") + "_TEMPLATE.png");
                if (texture == null) throw new InvalidOperationException("Missing manual art template " + index);
                var path = "Assets/_Project/Art/Materials/Manual/Leaf" + index.ToString("00") + "_TEMPLATE.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
                mat.SetTexture("_BaseMap", texture); mat.SetColor("_BaseColor", Color.white);
                mat.SetTextureScale("_BaseMap", new Vector2(0.5f, 1));
                mat.SetTextureOffset("_BaseMap", new Vector2(index % 2 == 0 ? 0 : 0.5f, 0));
                mat.SetFloat("_Cull", 0); mat.SetFloat("_Smoothness", 0.1f);
                renderer.sharedMaterial = mat;
                EditorUtility.SetDirty(mat);
            }
        }
    }
}
