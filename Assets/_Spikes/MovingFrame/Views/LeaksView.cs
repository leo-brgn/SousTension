using System.Collections.Generic;
using UnityEngine;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// Passive view of the hull leaks (E6-02): a water jet on the wall at each open leak, bigger for a bigger leak (small = rivet, thin jet;
    /// medium / large = deformed plate, thick jet). It disappears when the server says the leak is sealed. Built from primitives for the spike.
    /// </summary>
    public sealed class LeaksView : MonoBehaviour
    {
        private MovingFrameModel _model;
        private Transform _boat;
        private readonly Dictionary<int, Transform> _jets = new Dictionary<int, Transform>();
        private readonly List<int> _stale = new List<int>();

        public void Bind(MovingFrameModel model, Transform boat)
        {
            _model = model; _boat = boat;
        }

        private void Update()
        {
            if (_model == null) return;
            _stale.Clear();
            foreach (var id in _jets.Keys) _stale.Add(id);
            foreach (var leak in _model.Leaks)
            {
                _stale.Remove(leak.Id);
                if (!_jets.TryGetValue(leak.Id, out var jet)) { jet = Create(leak); _jets[leak.Id] = jet; }
                float thickness = 0.04f + 0.05f * leak.Size;
                float length = 0.5f + 0.25f * leak.Size;
                jet.localScale = new Vector3(length, thickness, thickness);
                // jet shoots from the wall (x = +/-3) towards the middle of the boat
                float dir = leak.X > 0f ? -1f : 1f;
                jet.localPosition = new Vector3(leak.X + dir * length / 2f, 0.7f, leak.Z);
            }
            foreach (var id in _stale) { Destroy(_jets[id].gameObject); _jets.Remove(id); }
        }

        private Transform Create(LeakState leak)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Leak" + leak.Id;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(_boat, false);
            go.GetComponent<Renderer>().material.color = new Color(0.45f, 0.75f, 0.95f);
            return go.transform;
        }
    }
}
