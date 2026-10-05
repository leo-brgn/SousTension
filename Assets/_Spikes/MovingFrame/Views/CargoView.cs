using System.Collections.Generic;
using UnityEngine;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// Passive view of the cargo: light crates (cubes) and the heavy fuel flask (cylinder), drawn in boat-local
    /// space (children of the boat). Positions come from the model, which interpolates authoritative snapshots
    /// and uses the local prediction for cargo the local player is carrying. Pending heavy items are tinted amber.
    /// </summary>
    public sealed class CargoView : MonoBehaviour
    {
        private const double InterpolationDelay = 0.1;

        private MovingFrameModel _model;
        private IClockService _clock;
        private Transform _boat;
        private readonly Dictionary<string, Transform> _items = new Dictionary<string, Transform>();

        public void Bind(MovingFrameModel model, IClockService clock, Transform boat)
        {
            _model = model; _clock = clock; _boat = boat;
        }

        private void Update()
        {
            if (_model == null || !_model.HasServerTime) return;
            double renderTime = _model.EstimateServerTime(_clock.Now) - InterpolationDelay;
            foreach (var c in _model.Cargo)
            {
                if (!_model.TryGetCargoRenderPosition(c.Id, renderTime, out float x, out float z)) continue;
                var t = Get(c);
                if (t.gameObject.activeSelf != c.Active) t.gameObject.SetActive(c.Active);   // a used hull patch is away until it respawns
                if (!c.Active) continue;
                bool carried = c.Carriers.Length > 0;
                t.localPosition = new Vector3(x, (c.Heavy ? 0.4f : 0.25f) + (carried ? 1.0f : 0f), z);
                var r = t.GetComponent<Renderer>();
                r.material.color = c.Kind == "bucket" ? new Color(0.2f, 0.55f, 0.75f) : c.Kind == "patch" ? new Color(0.55f, 0.65f, 0.72f) : c.Pending != "" ? new Color(0.95f, 0.7f, 0.1f) : c.Heavy ? new Color(0.35f, 0.35f, 0.4f) : new Color(0.6f, 0.4f, 0.2f);
            }
        }

        private Transform Get(CargoState c)
        {
            if (_items.TryGetValue(c.Id, out var t)) return t;
            var go = GameObject.CreatePrimitive(c.Heavy ? PrimitiveType.Cylinder : PrimitiveType.Cube);
            go.name = "Cargo_" + c.Id;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(_boat, false);
            go.transform.localScale = c.Heavy ? new Vector3(0.6f, 0.4f, 0.6f) : c.Kind == "bucket" ? new Vector3(0.3f, 0.3f, 0.3f) : c.Kind == "patch" ? new Vector3(0.4f, 0.12f, 0.4f) : new Vector3(0.5f, 0.5f, 0.5f);
            _items[c.Id] = go.transform;
            return go.transform;
        }
    }
}
