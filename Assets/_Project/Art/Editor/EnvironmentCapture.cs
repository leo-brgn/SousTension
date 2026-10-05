using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SousTension.EditorTools
{
    public static class EnvironmentCapture
    {
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Art/Scenes/BoatEnvironment.unity");
            Directory.CreateDirectory("scratch_out/preview/UnityEnvironment");
            var camera = GameObject.Find("MainCamera").GetComponent<Camera>();
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            Capture(camera, "Reactor");
            camera.transform.position = new Vector3(-0.2f, 1.65f, -5f);
            camera.transform.LookAt(new Vector3(2.18f, 1.05f, -7.3f));
            Capture(camera, "Central");
            camera.transform.position = new Vector3(0, 1.65f, 7.25f);
            camera.transform.LookAt(new Vector3(-1.7f, 0.90f, 5.9f));
            Capture(camera, "Machines");
            camera.transform.position = new Vector3(-.15f, 1.65f, -1.6f);
            camera.transform.LookAt(new Vector3(-2.13f, 1.15f, -2.1f));
            Capture(camera, "Radio");
            camera.transform.position = new Vector3(0, 1.65f, 10.8f);
            camera.transform.LookAt(new Vector3(-2.3f, 1.15f, 9.6f));
            Capture(camera, "Living");
            camera.transform.position = new Vector3(-1.95f, 1.65f, 11.45f);
            camera.transform.LookAt(new Vector3(-1.95f, 1.0f, 12.95f));
            Capture(camera, "Airlock");
            var overview = GameObject.Find("OverviewCamera").GetComponent<Camera>();
            overview.aspect = 1900f / 1100f;
            overview.orthographicSize = 9.7f;
            var cut = GameObject.Find("BoatEnvironment").GetComponentsInChildren<MeshRenderer>(true)
                .Where(r => r.transform.parent.name.Contains("EnvironmentHull_2m") && (r.name == "PortWall" || r.name == "Roof")).ToArray();
            foreach (var renderer in cut) renderer.enabled = false;
            var sun = new GameObject("PreviewKey").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(48, -30, 0);
            Capture(overview, "Boat_cutaway");
            UnityEngine.Object.DestroyImmediate(sun.gameObject);
            foreach (var renderer in cut) renderer.enabled = true;
            camera.transform.position = new Vector3(-0.25f, 1.65f, 3.2f);
            camera.transform.LookAt(new Vector3(2.3f, 1.1f, 1.9f));
            var lamps = GameObject.Find("BoatEnvironment").GetComponentsInChildren<Light>(true);
            for (var state = 1; state <= 3; state++)
            {
                var label = state == 1 ? "LowVoltage" : state == 2 ? "Emergency" : "Blackout";
                foreach (var lamp in lamps)
                {
                    if (lamp.name == "RoomLight")
                    {
                        lamp.enabled = state == 1;
                        lamp.color = new Color(1, 0.49f, 0.10f); lamp.intensity = 1;
                    }
                    if (lamp.name == "EmergencyLight") lamp.enabled = state == 2;
                }
                foreach (var lens in GameObject.Find("BoatEnvironment").GetComponentsInChildren<MeshRenderer>())
                    if (lens.name == "Lens")
                    {
                        var emergency = lens.transform.parent.name.Contains("EmergencyLamp");
                        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Effects/Lamp_"
                            + (state == 3 || (emergency && state == 1) || (!emergency && state == 2) ? "Blackout" : label) + ".mat");
                        if (material != null) lens.sharedMaterial = material;
                    }
                RenderSettings.ambientLight = state == 3 ? Color.black : new Color(0.025f, 0.025f, 0.02f);
                Capture(camera, label);
            }
            Debug.Log("ENVIRONMENT_CAPTURE_PASS: ten native Unity URP renders.");
        }

        private static void Capture(Camera camera, string name)
        {
            var target = new RenderTexture(1900, 1100, 24, RenderTextureFormat.ARGB32);
            target.Create();
            camera.aspect = 1900f / 1100f;
            try
            {
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                RenderPipeline.SubmitRenderRequest(camera, request);
                var previous = RenderTexture.active;
                RenderTexture.active = target;
                var image = new Texture2D(1900, 1100, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1900, 1100), 0, 0);
                image.Apply();
                File.WriteAllBytes("scratch_out/preview/UnityEnvironment/" + name + ".png", image.EncodeToPNG());
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(image);
                Debug.Log("UNITY CAPTURE " + name);
            }
            finally { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        }
    }
}
