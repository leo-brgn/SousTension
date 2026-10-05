using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace SousTension.EditorTools
{
    /// <summary>Capture saved baked galleries without modifying or saving their lighting.</summary>
    public static class AllAssetSceneCapture
    {
        [Serializable] private sealed class SceneList { public Entry[] scenes; }
        [Serializable] private sealed class Entry { public string path, kit; public int prefabCount; }
        [Serializable] private sealed class Report { public int count; public Shot[] images; public string renderer = "Unity URP SingleCameraRequest; saved lightmaps retained"; }
        [Serializable] private sealed class Shot
        {
            public string scenePath, kit, imagePath, view;
            public int prefabCount, lightmapCount, lightmappedRendererCount, width = 1900, height = 1100;
            public bool postProcessing;
        }
        [Serializable] private sealed class Layout { public Room[] rooms; public Station[] stations; }
        [Serializable] private sealed class Room { public string id; public float start, end; }
        [Serializable] private sealed class Station { public string room; public float[] position; }
        private const string Output = "scratch_out/preview/AllAssets";
        private static readonly List<Shot> Shots = new List<Shot>();

        public static void Run()
        {
            RunSelected(null);
        }
        public static void RunSelected(string[] selected)
        {
            var entries = JsonUtility.FromJson<SceneList>(File.ReadAllText("scratch_out/all-asset-scenes.json"));
            if (entries.scenes == null || entries.scenes.Length == 0) throw new InvalidOperationException("No baked scenes supplied.");
            Directory.CreateDirectory(Output); Shots.Clear();
            if (selected != null && File.Exists(Output + "/screenshots.json"))
                Shots.AddRange(JsonUtility.FromJson<Report>(File.ReadAllText(Output + "/screenshots.json")).images.Where(s => !selected.Contains(s.scenePath)));
            foreach (var entry in entries.scenes)
            {
                if (selected != null && !selected.Contains(entry.path)) continue;
                if (!File.Exists(entry.path)) throw new FileNotFoundException("Saved scene missing", entry.path);
                var scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);
                var cameras = Components<Camera>(scene);
                var camera = cameras.FirstOrDefault(c => c.name == "MainCamera");
                if (camera == null) throw new InvalidOperationException(entry.path + ": MainCamera missing");
                if (LightmapSettings.lightmaps.Length == 0) throw new InvalidOperationException(entry.path + ": saved baked lightmaps missing");
                Capture(scene, entry, camera, Path.GetFileNameWithoutExtension(entry.path), "gallery");
                if (entry.kit == "BoatEnvironment") BoatShots(scene, entry, camera);
                else DetailShots(scene, entry, camera);
            }
            File.WriteAllText(Output + "/screenshots.json", JsonUtility.ToJson(new Report { count = Shots.Count, images = Shots.ToArray() }, true));
            Debug.Log("ALL_ASSET_CAPTURE_PASS " + Shots.Count + " baked Unity screenshots; scene files were not saved or modified on disk.");
        }

        private static T[] Components<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();

        private static void DetailShots(Scene scene, Entry entry, Camera camera)
        {
            var gallery = scene.GetRootGameObjects().First(g => g.name.StartsWith("Gallery_"));
            var names = new[] { "MolossExterior_Wear0", "CommanderVarga", "DivingSuit", "PeriscopeInterior", "MarineToiletSevenValve", "SignalEmitterAssembled", "NuclearFuelTransportCask", "CargoTorpedoTube", "CompartmentCeilingLight", "EmergencyLamp" };
            foreach (Transform asset in gallery.transform)
            {
                if (entry.kit != "World" && !names.Contains(asset.name)) continue;
                var visible = asset.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && !(r is ParticleSystemRenderer)).ToArray();
                if (visible.Length == 0) continue;
                var bounds = visible[0].bounds; foreach (var r in visible.Skip(1)) bounds.Encapsulate(r.bounds);
                var others = gallery.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && !r.transform.IsChildOf(asset)).ToArray();
                var floor = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "StaticWhiteGalleryFloor")?.GetComponent<Renderer>();
                bool underside = asset.name == "CompartmentCeilingLight";
                var position = camera.transform.position; var rotation = camera.transform.rotation; float size = camera.orthographicSize;
                try
                {
                    foreach (var r in others) r.enabled = false;
                    AllAssetSceneBuilder.ReframeCamera(camera, bounds);
                    if (underside)
                    {
                        if (floor != null) floor.enabled = false;
                        camera.transform.position = bounds.center + new Vector3(.65f,-.65f,.9f).normalized * (Mathf.Max(bounds.size.magnitude,2)*2+10);
                        camera.transform.LookAt(bounds.center);
                    }
                    Capture(scene, entry, camera, "Detail_" + asset.name, "detail " + asset.name + "; neighboring models hidden temporarily, saved lightmaps retained");
                }
                finally
                {
                    foreach (var r in others) r.enabled = true;
                    if (underside && floor != null) floor.enabled = true;
                    camera.transform.SetPositionAndRotation(position, rotation); camera.orthographicSize = size;
                }
            }
        }

        private static void BoatShots(Scene scene, Entry entry, Camera camera)
        {
            var layout = JsonUtility.FromJson<Layout>(File.ReadAllText("Assets/_Project/Art/Models/Environment/boat_layout.json"));
            Vector3 originalPosition = camera.transform.position; Quaternion originalRotation = camera.transform.rotation;
            float fov = camera.fieldOfView;
            try
            {
                camera.fieldOfView = 78;
                foreach (var room in layout.rooms)
                {
                    var station = layout.stations.FirstOrDefault(s => s.room == room.id);
                    float z = -(room.start + room.end) * .5f;
                    camera.transform.position = new Vector3(-.25f, 1.65f, z + .6f);
                    Vector3 target = station != null && station.position.Length == 3
                        ? new Vector3(-station.position[0], 1.10f, -station.position[1])
                        : new Vector3(1.9f, 1.0f, z - .9f);
                    if ((target - camera.transform.position).sqrMagnitude < .2f) target += Vector3.right;
                    camera.transform.LookAt(target);
                    Capture(scene, entry, camera, "BoatEnvironment_Lit_" + room.id, "interior " + room.id);
                }
                var overview = Components<Camera>(scene).FirstOrDefault(c => c.name == "OverviewCamera");
                if (overview == null) throw new InvalidOperationException("Boat OverviewCamera missing");
                var hidden = Components<MeshRenderer>(scene).Where(r => r.enabled &&
                    (r.name == "PortWall" || r.name == "Roof") && r.transform.parent != null &&
                    r.transform.parent.name.Contains("EnvironmentHull_2m")).ToArray();
                var sun = new GameObject("PreviewSun").AddComponent<Light>();
                try
                {
                    foreach (var renderer in hidden) renderer.enabled = false;
                    sun.type = LightType.Directional; sun.lightmapBakeType = LightmapBakeType.Realtime; sun.intensity = 1.2f;
                    sun.transform.rotation = Quaternion.Euler(48, -30, 0);
                    Capture(scene, entry, overview, "BoatEnvironment_Lit_cutaway", "temporary cutaway with realtime PreviewSun; baked lightmaps retained");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(sun.gameObject);
                    foreach (var renderer in hidden) renderer.enabled = true;
                }
            }
            finally { camera.transform.SetPositionAndRotation(originalPosition, originalRotation); camera.fieldOfView = fov; }
        }

        private static void Capture(Scene scene, Entry entry, Camera camera, string name, string view)
        {
            const int width = 1900, height = 1100;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            target.Create(); var previous = RenderTexture.active; float aspect = camera.aspect;
            Texture2D image = null;
            try
            {
                camera.aspect = (float)width / height;
                // Warm the loaded scene/pipeline, then capture with its lightmaps and volume settings.
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                string path = Output + "/" + name + ".png";
                File.WriteAllBytes(path, image.EncodeToPNG());
                var additional = camera.GetUniversalAdditionalCameraData();
                Shots.Add(new Shot { scenePath = entry.path, kit = entry.kit, prefabCount = entry.prefabCount,
                    imagePath = Path.GetFullPath(path), view = view, lightmapCount = LightmapSettings.lightmaps.Length,
                    lightmappedRendererCount = Components<Renderer>(scene).Count(r => r.lightmapIndex >= 0 && r.lightmapIndex < LightmapSettings.lightmaps.Length),
                    postProcessing = additional.renderPostProcessing });
                Debug.Log("ALL_ASSET_SCREENSHOT " + path + " lightmaps=" + LightmapSettings.lightmaps.Length);
            }
            finally
            {
                camera.aspect = aspect; RenderTexture.active = previous;
                if (image != null) UnityEngine.Object.DestroyImmediate(image);
                target.Release(); UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
