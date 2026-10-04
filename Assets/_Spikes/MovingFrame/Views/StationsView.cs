using UnityEngine;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// Passive view of the two interlock keys (one at each end of the boat). Colour = authoritative state:
    /// grey idle, amber pressed (waiting for the other player), green flash on success, red flash on timeout.
    /// Positions mirror STATIONS in server/modules/index.js.
    /// </summary>
    public sealed class StationsView : MonoBehaviour
    {
        private static readonly Vector3[] Positions = { new Vector3(0f, 0f, -9f), new Vector3(0f, 0f, 9f) };

        private MovingFrameModel _model;
        private readonly Renderer[] _renderers = new Renderer[2];
        private int _lastResultTick = -1;
        private float _flashUntil;
        private Color _flashColor;

        public void Bind(MovingFrameModel model, Transform boat)
        {
            _model = model;
            for (int i = 0; i < 2; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = "InterlockKey" + (i == 0 ? "A" : "B");
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(boat, false);
                go.transform.localPosition = Positions[i] + new Vector3(0f, 0.6f, 0f);
                go.transform.localScale = new Vector3(0.5f, 0.6f, 0.5f);
                _renderers[i] = go.GetComponent<Renderer>();
            }
        }

        private void Update()
        {
            if (_model == null) return;
            var il = _model.Interlock;
            if (il.ResultTick != _lastResultTick && il.Result != "none")
            {
                _lastResultTick = il.ResultTick;
                _flashUntil = Time.time + 1.0f;
                _flashColor = il.Result == "success" ? new Color(0.2f, 0.9f, 0.3f) : new Color(0.9f, 0.2f, 0.2f);
            }
            for (int i = 0; i < 2; i++)
            {
                bool pressed = (i == 0 ? il.RemainingA : il.RemainingB) > 0;
                _renderers[i].material.color = Time.time < _flashUntil ? _flashColor
                    : pressed ? new Color(0.95f, 0.7f, 0.1f) : new Color(0.5f, 0.5f, 0.5f);
            }
        }
    }
}
