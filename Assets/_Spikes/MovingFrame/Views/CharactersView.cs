using System.Collections.Generic;
using UnityEngine;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// Passive view: one capsule per player, positioned in the BOAT's local space (children of the boat root),
    /// so they ride the boat automatically. The local player uses the predicted position; remotes are
    /// interpolated 100 ms in the past. The first-person camera is a child of the local capsule.
    /// </summary>
    public sealed class CharactersView : MonoBehaviour
    {
        private const double InterpolationDelay = 0.1;

        private MovingFrameModel _model;
        private IClockService _clock;
        private KeyboardInputSource _look;
        private Transform _boat;
        private Transform _cameraRig;
        public static float CameraStabilization = ReadStabilization();

        private static float ReadStabilization()
        {
            foreach (var a in System.Environment.GetCommandLineArgs())
                if (a.StartsWith("--stab=") && float.TryParse(a.Substring(7), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v)) return Mathf.Clamp01(v);
            return 0.7f;
        }
        private readonly Dictionary<string, Transform> _characters = new Dictionary<string, Transform>();
        private readonly HashSet<string> _seen = new HashSet<string>();
        private readonly List<string> _stale = new List<string>();
        private readonly HashSet<string> _artCharacters = new HashSet<string>();               // remotes drawn with the 3D sailor (origin at the feet)
        private readonly Dictionary<string, Vector3> _lastPosition = new Dictionary<string, Vector3>();

        public void Bind(MovingFrameModel model, IClockService clock, KeyboardInputSource look, Transform boat, Camera camera)
        {
            _model = model; _clock = clock; _look = look; _boat = boat;
            _cameraRig = camera.transform;
        }

        // After BoatView has posed the boat this frame (it runs first: DefaultExecutionOrder), so the compensation uses the rotation that is drawn.
        private void LateUpdate()
        {
            if (_cameraRig == null || _cameraRig.parent == null || _model == null || string.IsNullOrEmpty(_model.LocalId)) return;
            // Comfort (E2-01): the camera sits in a hull that rolls and pitches; the eyes see the horizon of the room tilt while the inner ear feels nothing,
            // which is what makes people sick. The head compensates a share of the boat's tilt (a steadied head, as on a ship): the room still sways around
            // the player, the view does not. 0 = rigidly fixed to the boat (as before), 1 = a fully level camera. Option: --stab=0..1.
            var boatRotation = _cameraRig.parent.rotation;                       // the boat's rotation: the player has no rotation of their own
            _cameraRig.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.Inverse(boatRotation), CameraStabilization) * _cameraRig.rotation;
        }

        private void Update()
        {
            if (_model == null || !_model.HasServerTime || string.IsNullOrEmpty(_model.LocalId)) return;
            _look.UpdateLook();

            // Local
            var local = Get(_model.LocalId, isLocal: true);
            local.localPosition = new Vector3(_model.LocalX, 0.9f, _model.LocalZ);
            _cameraRig.SetParent(local, false);
            _cameraRig.localPosition = new Vector3(0f, 0.7f, 0f);
            _cameraRig.localRotation = Quaternion.Euler(_look.Pitch * Mathf.Rad2Deg, _look.Yaw * Mathf.Rad2Deg, 0f);

            // Remotes
            double renderTime = _model.EstimateServerTime(_clock.Now) - InterpolationDelay;
            var seen = _seen; seen.Clear();                                        // no allocation per frame
            foreach (var id in _model.RemoteIds)
            {
                seen.Add(id);
                if (!_model.TrySampleRemote(id, renderTime, out float x, out float z)) continue;
                var remote = Get(id, isLocal: false);
                if (_artCharacters.Contains(id))
                {
                    var now = new Vector3(x, 0f, z);
                    if (_lastPosition.TryGetValue(id, out var before) && (now - before).sqrMagnitude > 1e-6f)
                        remote.localRotation = Quaternion.Slerp(remote.localRotation, Quaternion.LookRotation(now - before), 10f * Time.deltaTime);   // faces where it walks
                    _lastPosition[id] = now;
                    remote.localPosition = now;
                }
                else remote.localPosition = new Vector3(x, 0.9f, z);
            }
            var stale = _stale; stale.Clear();
            foreach (var kv in _characters) if (kv.Key != _model.LocalId && !seen.Contains(kv.Key)) stale.Add(kv.Key);
            foreach (var id in stale) { Destroy(_characters[id].gameObject); _characters.Remove(id); }
        }

        private Transform Get(string id, bool isLocal)
        {
            if (_characters.TryGetValue(id, out var t)) return t;
            if (!isLocal)
            {
                var sailor = SpikeArt.Spawn(id.GetHashCode() % 2 == 0 ? "SailorBase" : "SailorFemale", _boat) ?? SpikeArt.Spawn("SailorBase", _boat);
                if (sailor != null)
                {
                    sailor.name = "Remote_" + id.Substring(0, Mathf.Min(6, id.Length));
                    _characters[id] = sailor.transform;
                    _artCharacters.Add(id);
                    return sailor.transform;
                }
            }
            var go = SpikePrimitives.Create(PrimitiveType.Capsule);
            go.name = (isLocal ? "Local_" : "Remote_") + id.Substring(0, Mathf.Min(6, id.Length));
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(_boat, false);
            var r = go.GetComponent<Renderer>();
            r.material.color = isLocal ? new Color(0.2f, 0.7f, 0.3f) : Color.HSVToRGB((Mathf.Abs(id.GetHashCode()) % 100) / 100f, 0.6f, 0.9f);
            if (isLocal) r.enabled = false; // first person: hide own body
            _characters[id] = go.transform;
            return go.transform;
        }
    }
}
