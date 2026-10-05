using UnityEngine;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// Passive view of the main electrical panel (E3-07): 20 breakers laid out as floor tiles 0.5 m apart (3 columns x 7 rows) at the stern on the
    /// left, mirroring BREAKERS in server/src/power.js (the spike interacts by position; E2-02 will replace it by a real panel). Colour: green
    /// closed, dark open, red tripped (a real incident). Built from primitives for the spike.
    /// </summary>
    public sealed class BreakerPanelView : MonoBehaviour
    {
        private const int Count = 20;
        private MovingFrameModel _model;
        private readonly Renderer[] _tiles = new Renderer[Count];

        public void Bind(MovingFrameModel model, Transform boat)
        {
            _model = model;
            for (int i = 0; i < Count; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Breaker" + i;
                InteractableTarget.Mark(go, InteractableIds.Breakers[i]);
                go.transform.SetParent(boat, false);
                go.transform.localPosition = new Vector3(-2.5f + 0.5f * (i % 3), 0.04f, -9.75f + 0.5f * (i / 3));
                go.transform.localScale = new Vector3(0.36f, 0.08f, 0.36f);
                _tiles[i] = go.GetComponent<Renderer>();
            }
        }

        private void Update()
        {
            if (_model == null) return;
            var grid = _model.Grid;
            for (int i = 0; i < Count; i++)
            {
                int s = grid.Valid && i < grid.Breakers.Length ? grid.Breakers[i] : 1;   // 0 open, 1 closed, 2 tripped
                _tiles[i].material.color = s == 2 ? new Color(0.9f, 0.15f, 0.12f) : s == 1 ? new Color(0.25f, 0.7f, 0.3f) : new Color(0.2f, 0.2f, 0.2f);
            }
        }
    }
}
