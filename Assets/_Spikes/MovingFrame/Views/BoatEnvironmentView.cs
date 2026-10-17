using System.Collections.Generic;
using SousTension.Sim;
using UnityEngine;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// The 3D boat (Assets/_Project/Art/Prefabs/BoatEnvironment.prefab, 28 m, six compartments) as the shell of the spike: hull, floors, bulkheads, hatches,
    /// baffles, pipes, lamps and furniture. The prefab is expressed in the art's frame (Blender -> Unity: x and z negated), so it is turned 180 degrees
    /// about the vertical to land in the boat-local frame of the simulation (x = right, z = forward, bow = +z): the geometry then equals
    /// <see cref="BoatLayout"/>. Passive view: the hatches follow the authoritative state of the bulkhead openings and the lights follow the
    /// electrical grid of their own compartment. The equipment placed by the art (RK-1 console, SCRAM lever, valves, pumps, panel...) is exposed to the other
    /// views, which animate its parts instead of building primitives.
    /// </summary>
    public sealed class BoatEnvironmentView : MonoBehaviour
    {
        public static BoatEnvironmentView Current { get; private set; }

        private const float HatchSeconds = 1.0f;
        private const float WheelTurns = 0.75f;

        private MovingFrameModel _model;
        private Transform _root;
        private readonly Dictionary<string, List<Transform>> _byAsset = new Dictionary<string, List<Transform>>();
        private readonly Transform[] _doors = new Transform[BoatLayout.BulkheadCount];
        private readonly Transform[] _wheels = new Transform[BoatLayout.BulkheadCount];
        private readonly Quaternion[] _openRotation = new Quaternion[BoatLayout.BulkheadCount];
        private readonly Quaternion[] _wheelRotation = new Quaternion[BoatLayout.BulkheadCount];
        private readonly float[] _closed = new float[BoatLayout.BulkheadCount];     // 0 = open, 1 = closed

        public Transform Root => _root;
        public IReadOnlyList<Light> RoomLights => _roomLights;
        public IReadOnlyList<Light> EmergencyLights => _emergencyLights;
        private readonly List<Light> _roomLights = new List<Light>();
        private readonly List<Light> _emergencyLights = new List<Light>();

        /// <summary>Instantiates the environment under <paramref name="boat"/>; null when the art is not in the catalog.</summary>
        public static BoatEnvironmentView TryCreate(Transform boat, MovingFrameModel model)
        {
            var env = SpikeArt.Spawn("Environment", boat, stripColliders: false);
            if (env == null) return null;
            env.name = "BoatEnvironment";
            env.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);        // art frame -> simulation frame
            var view = env.AddComponent<BoatEnvironmentView>();
            view.Bind(model, boat);
            return view;
        }

        private void Bind(MovingFrameModel model, Transform boat)
        {
            _model = model;
            _root = transform;
            Current = this;
            Index();
            KeepOnlyStructuralColliders();
            foreach (var l in GetComponentsInChildren<Light>(true))
            {
                if (l.name.StartsWith("Emergency")) _emergencyLights.Add(l); else _roomLights.Add(l);
                l.shadows = LightShadows.None;
            }
            for (int d = 0; d < BoatLayout.BulkheadCount; d++) BindHatch(d);
        }

        // ---- finding the equipment placed by the art -------------------------------------------------------------------------------------------

        // Every placed object is a wrapper named "<room>_<Asset>_<n>", e.g. "04_Reactor_ScramLever_302".
        private void Index()
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                string asset = AssetName(t.name);
                if (asset == null) continue;
                if (!_byAsset.TryGetValue(asset, out var list)) _byAsset[asset] = list = new List<Transform>();
                list.Add(t);
            }
        }

        private static string AssetName(string name)
        {
            // "<NN>_<Room>_<Asset>_<n>"
            int a = name.IndexOf('_');
            if (a != 2) return null;
            int b = name.IndexOf('_', a + 1);
            int c = name.LastIndexOf('_');
            if (b < 0 || c <= b) return null;
            for (int i = c + 1; i < name.Length; i++) if (!char.IsDigit(name[i])) return null;
            return name.Substring(b + 1, c - b - 1);
        }

        /// <summary>Boat-local position of a transform (x right, z forward, y up).</summary>
        public Vector3 BoatLocal(Transform t) => _root.parent.InverseTransformPoint(t.position);

        /// <summary>All placed instances of the art asset (e.g. "PrimaryValve"), sorted by boat-local z ascending then x ascending, then height.</summary>
        public List<Transform> All(string asset)
        {
            var result = new List<Transform>();
            if (_byAsset.TryGetValue(asset, out var list)) result.AddRange(list);
            result.Sort((p, q) =>
            {
                var a = BoatLocal(p); var b = BoatLocal(q);
                int c = a.z.CompareTo(b.z);
                if (c != 0) return c;
                c = a.x.CompareTo(b.x);
                return c != 0 ? c : a.y.CompareTo(b.y);
            });
            return result;
        }

        /// <summary>The placed instance of <paramref name="asset"/> nearest to a boat-local (x, z), or null.</summary>
        public Transform Nearest(string asset, float x, float z)
        {
            Transform best = null; float bestD = float.MaxValue;
            if (!_byAsset.TryGetValue(asset, out var list)) return null;
            foreach (var t in list)
            {
                var p = BoatLocal(t);
                float d = (p.x - x) * (p.x - x) + (p.z - z) * (p.z - z);
                if (d < bestD) { bestD = d; best = t; }
            }
            return best;
        }

        // ---- colliders: only the structure blocks the aim ray (a wall in front stops it); the equipment gets its own aim boxes --------------------

        private void KeepOnlyStructuralColliders()
        {
            foreach (var c in GetComponentsInChildren<Collider>(true))
            {
                string n = c.name;
                bool structural = !c.isTrigger && (n.StartsWith("Bulkhead") || n.StartsWith("PrivacyBaffle") || n.StartsWith("EnvironmentHull") || n.StartsWith("HullModule"));
                if (!structural) DestroyImmediate(c);
            }
        }

        // ---- hatches ---------------------------------------------------------------------------------------------------------------------------

        private void BindHatch(int d)
        {
            var wrapper = Nearest("HatchDoor", BoatLayout.HatchX[d], BoatLayout.BulkheadZ[d]);
            if (wrapper == null) return;
            _doors[d] = wrapper;
            _openRotation[d] = wrapper.localRotation;                       // the art places the leaf swung open against the wall; closed = no yaw
            var wheel = SpikeArt.Find(wrapper, "Wheel");
            _wheels[d] = wheel;
            if (wheel != null) _wheelRotation[d] = wheel.localRotation;
            string id = "hatch" + d;
            SpikeArt.MakeAimable(SpikeArt.Find(wrapper, "Door"), id, 0.1f);
            SpikeArt.MakeAimable(wheel, id, 0.1f);
            _closed[d] = 0f;
        }

        private void Update()
        {
            if (_model == null) return;
            var water = _model.Water;
            for (int d = 0; d < _doors.Length; d++)
            {
                if (_doors[d] == null) continue;
                bool open = !water.Valid || d >= water.DoorOpen.Length || water.DoorOpen[d];
                _closed[d] = Mathf.MoveTowards(_closed[d], open ? 0f : 1f, Time.deltaTime / HatchSeconds);
                float e = Mathf.SmoothStep(0f, 1f, _closed[d]);
                _doors[d].localRotation = Quaternion.Slerp(_openRotation[d], Quaternion.identity, e);
                if (_wheels[d] != null) _wheels[d].localRotation = _wheelRotation[d] * Quaternion.AngleAxis(e * WheelTurns * 360f, Vector3.up);
            }
        }

        private void OnDestroy() { if (Current == this) Current = null; }
    }
}
