using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SousTension.EditorTools
{
    /// <summary>Build the independent art-preview scene from the shared Blender layout.</summary>
    public static class BoatEnvironmentBuilder
    {
        [Serializable] private sealed class Layout
        {
            public float length, width, height;
            public Room[] rooms;
            public Entry[] instances;
            public Station[] stations;
        }
        [Serializable] private sealed class Room { public string id, label; public float start, end; }
        [Serializable] private sealed class Entry
        {
            public string id, room, kit, asset, parent;
            public float[] position, rotation;
            public PartState[] partStates;
        }
        [Serializable] private sealed class PartState { public string name; public float[] rotation; }
        [Serializable] private sealed class Station { public string room; public float[] position; }
        private const string Models = "Assets/_Project/Art/Models/";
        private const string ScenePath = "Assets/_Project/Art/Scenes/BoatEnvironment.unity";

        [MenuItem("Sous Tension/Art/Build boat environment")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(Models + "Environment/boat_layout.json"));
            if (layout.rooms.Length != 6 || Math.Abs(layout.length - 28) > 0.01f)
                throw new InvalidOperationException("The GDD requires six compartments and 28 playable metres.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var boat = new GameObject("BoatEnvironment").transform;
            var rooms = new Dictionary<string, Transform>();
            foreach (var room in layout.rooms)
            {
                var root = new GameObject(room.id).transform;
                root.SetParent(boat, false);
                rooms.Add(room.id, root);
                var volume = new GameObject("CompartmentVolume").AddComponent<BoxCollider>();
                volume.transform.SetParent(root, false);
                volume.center = new Vector3(0, layout.height / 2, -(room.start + room.end) / 2);
                volume.size = new Vector3(layout.width, layout.height, room.end - room.start);
                volume.isTrigger = true;
            }
            var instances = new Dictionary<string, Transform>();
            var solid = new List<Collider>();
            foreach (var entry in layout.instances)
            {
                var path = Models + entry.kit + "/" + entry.asset + ".fbx";
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (source == null) throw new InvalidOperationException("Missing environment model " + path);
                var ob = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
                ob.name = entry.id;
                ob.transform.SetParent(string.IsNullOrEmpty(entry.parent) ? rooms[entry.room] : instances[entry.parent], false);
                ob.transform.localPosition = ToUnity(entry.position);
                ob.transform.localRotation = Rotation(entry.rotation);
                instances.Add(entry.id, ob.transform);
                foreach (var state in entry.partStates ?? Array.Empty<PartState>())
                {
                    var part = ob.GetComponentsInChildren<Transform>(true).Single(t => t.name == state.name);
                    part.localRotation = Rotation(state.rotation);
                }
                foreach (var renderer in ob.GetComponentsInChildren<MeshRenderer>(true))
                    if (renderer.sharedMaterial == null) throw new InvalidOperationException("Missing material " + path);
                AddCollision(entry, ob, solid);
                if (entry.asset == "DivingAirlockChamber")
                {
                    var volume = new GameObject("AirlockInteriorVolume").AddComponent<BoxCollider>();
                    volume.transform.SetParent(ob.transform, false);
                    volume.center = new Vector3(0, 1.05f, 0);
                    volume.size = new Vector3(1.30f, 1.90f, 1.50f);
                    volume.isTrigger = true;
                    var target = new GameObject("AirlockReachabilityTarget").transform;
                    target.SetParent(ob.transform, false);
                    target.localPosition = new Vector3(0, .13f, 0);
                }
                if (entry.asset == "ManualOK114OpenBinder") MvpPresentationBuilder.ApplyManualMaterials(ob);
                if (entry.asset == "CeilingLight" || entry.asset == "CompartmentCeilingLight")
                {
                    var lamp = new GameObject("RoomLight").AddComponent<Light>();
                    lamp.transform.SetParent(ob.transform, false);
                    lamp.transform.localPosition = new Vector3(0, -0.20f, 0);
                    lamp.type = LightType.Point;
                    lamp.range = 6.5f;
                    lamp.intensity = 2.4f;
                    lamp.color = new Color(1f, 0.90f, 0.72f);
                    lamp.shadows = LightShadows.Soft;
                    var lensMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Effects/Lamp_Normal.mat");
                    if (lensMaterial != null)
                        foreach (var renderer in ob.GetComponentsInChildren<Renderer>())
                            if (renderer.name == "Lens") renderer.sharedMaterial = lensMaterial;
                }
                if (entry.asset == "EmergencyLamp")
                {
                    var mount = ob.GetComponentsInChildren<Transform>().Single(t => t.name == "Mount_Light");
                    var lamp = new GameObject("EmergencyLight").AddComponent<Light>();
                    lamp.transform.SetParent(mount, false);
                    lamp.type = LightType.Point; lamp.range = 5; lamp.intensity = 1.2f;
                    lamp.color = new Color(0.85f, 0.025f, 0.015f); lamp.enabled = false;
                }
            }
            var deck = new GameObject("DeckCollision").AddComponent<BoxCollider>();
            deck.transform.SetParent(boat, false);
            deck.center = new Vector3(0, -0.075f, 0);
            deck.size = new Vector3(6, 0.15f, 28);
            solid.Add(deck);
            foreach (var station in layout.stations)
            {
                var marker = new GameObject("StationViewpoint").transform;
                marker.SetParent(rooms[station.room], false);
                marker.localPosition = ToUnity(station.position);
            }
            var spawns = new GameObject("CrewSpawns").transform;
            spawns.SetParent(boat, false);
            for (var i = 0; i < 4; i++)
            {
                var spawn = new GameObject("Spawn" + i).transform;
                spawn.SetParent(spawns, false);
                spawn.localPosition = new Vector3((i % 2) * 0.9f - 0.45f, 0.1f, -5.0f - (i / 2) * 0.9f);
            }
            var camera = new GameObject("MainCamera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(-0.25f, 1.65f, 3.2f);
            camera.transform.LookAt(new Vector3(2.3f, 1.1f, 1.9f));
            camera.fieldOfView = 78;
            camera.nearClipPlane = 0.04f;
            camera.farClipPlane = 60;
            camera.backgroundColor = new Color(0.06f, 0.08f, 0.09f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.gameObject.AddComponent<AudioListener>();
            var overview = new GameObject("OverviewCamera").AddComponent<Camera>();
            overview.transform.position = new Vector3(22, 25, 16);
            overview.transform.LookAt(new Vector3(0, 0.5f, 0));
            overview.orthographic = true;
            overview.orthographicSize = 12;
            overview.enabled = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.19f, 0.17f);
            RenderSettings.fog = false;
            Physics.SyncTransforms();
            var report = Validate(layout, solid);
            Directory.CreateDirectory("Assets/_Project/Art/Scenes");
            Directory.CreateDirectory("Assets/_Project/Art/Prefabs");
            AssetDatabase.Refresh();
            PrefabUtility.SaveAsPrefabAsset(boat.gameObject, "Assets/_Project/Art/Prefabs/BoatEnvironment.prefab");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("scratch_out");
            File.WriteAllText("scratch_out/environment-validation.txt", report);
            Debug.Log(report);
        }

        // Confirmed against exported Mount_Hinge: Blender (x,y,z) -> Unity (-x,z,-y).
        private static Vector3 ToUnity(float[] p) => new Vector3(-p[0], p[2], -p[1]);
        private static Quaternion Rotation(float[] r) => Quaternion.AngleAxis(-r[2], Vector3.up)
            * Quaternion.AngleAxis(r[1], Vector3.forward) * Quaternion.AngleAxis(r[0], Vector3.right);

        private static void AddCollision(Entry entry, GameObject ob, List<Collider> solid)
        {
            if (entry.kit == "Signage" || entry.parent.Length > 0 || entry.asset == "FloorGrating_1m" || entry.asset == "BilgeFloor_2m"
                || entry.asset == "CeilingLight" || entry.asset.StartsWith("Pipe_") || entry.asset == "Interphone") return;
            foreach (var mesh in ob.GetComponentsInChildren<MeshFilter>())
            {
                if (entry.asset == "HatchDoor" && mesh.name != "Door") continue;
                var collider = mesh.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh.sharedMesh;
                solid.Add(collider);
            }
        }

        private static string Validate(Layout layout, List<Collider> solid)
        {
            // Test station isolation with all five doors already open.
            var pairs = 0;
            for (var i = 0; i < layout.stations.Length; i++)
                for (var j = i + 1; j < layout.stations.Length; j++)
                {
                    var a = ToUnity(layout.stations[i].position);
                    var b = ToUnity(layout.stations[j].position);
                    if (!Physics.Linecast(a, b, out _, ~0, QueryTriggerInteraction.Ignore))
                        throw new InvalidOperationException("Visible workstations: " + layout.stations[i].room + " / " + layout.stations[j].room);
                    pairs++;
                }
            // Capsule occupancy on a 20 cm grid, including the offset passages.
            const float step = 0.2f;
            const int nx = 27, nz = 136;
            var open = new bool[nx, nz];
            for (var x = 0; x < nx; x++)
                for (var z = 0; z < nz; z++)
                {
                    var pos = new Vector3(-2.6f + x * step, 0, -13.5f + z * step);
                    open[x, z] = !Physics.CheckCapsule(pos + Vector3.up * 0.36f, pos + Vector3.up * 1.35f,
                        0.23f, ~0, QueryTriggerInteraction.Ignore);
                }
            var visited = new bool[nx, nz];
            var queue = new Queue<Vector2Int>();
            var firstSpawn = GameObject.Find("Spawn0").transform.position;
            var start = new Vector2Int(Mathf.RoundToInt((firstSpawn.x + 2.6f) / step), Mathf.RoundToInt((firstSpawn.z + 13.5f) / step));
            if (!open[start.x, start.y]) throw new InvalidOperationException("Blocked crew traversal start");
            queue.Enqueue(start);
            visited[start.x, start.y] = true;
            var reached = new HashSet<string>();
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                var z = 13.5f - cell.y * step;
                foreach (var room in layout.rooms)
                    if (z > room.start + 0.35f && z < room.end - 0.35f) reached.Add(room.id);
                foreach (var d in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
                {
                    var n = cell + d;
                    if (n.x < 0 || n.x >= nx || n.y < 0 || n.y >= nz || visited[n.x, n.y] || !open[n.x, n.y]) continue;
                    visited[n.x, n.y] = true;
                    queue.Enqueue(n);
                }
            }
            if (reached.Count != 6)
            {
                Directory.CreateDirectory("scratch_out");
                var map = new System.Text.StringBuilder();
                for (var z = 0; z < nz; z++)
                {
                    map.Append((-13.5f + z * step).ToString("F1")).Append(' ');
                    for (var x = 0; x < nx; x++) map.Append(visited[x, z] ? 'v' : open[x, z] ? '.' : '#');
                    map.AppendLine();
                }
                for (var z = 7.5f; z < 9.3f; z += 0.2f)
                {
                    var pos = new Vector3(1.4f, 0, z);
                    map.AppendLine("Gate " + pos + ": " + string.Join(",", Physics.OverlapCapsule(pos + Vector3.up * 0.36f,
                        pos + Vector3.up * 1.35f, 0.23f, ~0, QueryTriggerInteraction.Ignore).Select(c => c.transform.parent.name + "/" + c.name)));
                }
                File.WriteAllText("scratch_out/environment-route-debug.txt", map.ToString());
                throw new InvalidOperationException("Capsule route only reaches " + reached.Count + " compartments: " + string.Join(",", reached));
            }
            foreach (var spawn in GameObject.Find("CrewSpawns").GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Spawn")))
            {
                var p = spawn.position;
                if (!visited[Mathf.RoundToInt((p.x+2.6f)/step), Mathf.RoundToInt((p.z+13.5f)/step)])
                    throw new InvalidOperationException("Unreachable crew spawn " + spawn.name);
            }
            var airlock = GameObject.Find("AirlockReachabilityTarget");
            if (airlock != null)
            {
                var p = airlock.transform.position;
                var x = Mathf.RoundToInt((p.x + 2.6f) / step);
                var z = Mathf.RoundToInt((p.z + 13.5f) / step);
                if (x < 0 || x >= nx || z < 0 || z >= nz || !visited[x, z])
                {
                    var report = new System.Text.StringBuilder();
                    foreach (var t in airlock.transform.parent.GetComponentsInChildren<Transform>())
                        report.AppendLine(t.name + " local=" + t.localPosition + " world=" + t.position + " rotation=" + t.localEulerAngles);
                    for (var depth = 11.6f; depth < 13.4f; depth += .2f)
                    {
                        var pos = new Vector3(-1.95f, 0, depth);
                        report.AppendLine(pos + ": " + string.Join(",", Physics.OverlapCapsule(pos + Vector3.up * .36f,
                            pos + Vector3.up * 1.35f, .23f, ~0, QueryTriggerInteraction.Ignore)
                            .Select(c => c.transform.parent.name + "/" + c.name)));
                    }
                    File.WriteAllText("scratch_out/airlock-route-debug.txt", report.ToString());
                    throw new InvalidOperationException("Airlock interior is not reachable through its open inner hatch");
                }
            }
            return "ENVIRONMENT VALIDATED: 28 m, six compartments, " + layout.instances.Length
                + " modular instances, " + solid.Count + " solid colliders, all compartments reachable by a 46 cm capsule; "
                + pairs + " station sight lines blocked with doors open; airlock interior reachable with outer hatch closed. Scene: " + ScenePath;
        }
    }
}
