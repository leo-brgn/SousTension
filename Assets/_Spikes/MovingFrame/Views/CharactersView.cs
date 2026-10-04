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
        private readonly Dictionary<string, Transform> _characters = new Dictionary<string, Transform>();

        public void Bind(MovingFrameModel model, IClockService clock, KeyboardInputSource look, Transform boat, Camera camera)
        {
            _model = model; _clock = clock; _look = look; _boat = boat;
            _cameraRig = camera.transform;
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
            var seen = new HashSet<string>();
            foreach (var id in _model.RemoteIds)
            {
                seen.Add(id);
                if (_model.TrySampleRemote(id, renderTime, out float x, out float z))
                    Get(id, isLocal: false).localPosition = new Vector3(x, 0.9f, z);
            }
            var stale = new List<string>();
            foreach (var kv in _characters) if (kv.Key != _model.LocalId && !seen.Contains(kv.Key)) stale.Add(kv.Key);
            foreach (var id in stale) { Destroy(_characters[id].gameObject); _characters.Remove(id); }
        }

        private Transform Get(string id, bool isLocal)
        {
            if (_characters.TryGetValue(id, out var t)) return t;
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
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
