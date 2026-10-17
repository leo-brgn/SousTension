using SousTension.Sim;
using UnityEngine;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// Passive view of the water in the boat (E6-01): one translucent slab per compartment whose height follows the authoritative fill
    /// fraction (a full compartment is 2 m deep). Compartment layout mirrors WATER_COMPARTMENTS in server/src/water.js (six compartments of
    /// 4/6/4/4/4/6 m from the bow, +z, to the stern: <see cref="BoatLayout"/>). The boat's trim and list are applied by BoatView. Built from primitives
    /// for the spike (the water is an effect, not a model).
    /// </summary>
    public sealed class WaterView : MonoBehaviour
    {
        private const int Compartments = 6;
        private const float Depth = 2f;
        private const float BilgeFloorY = -0.4f;                // the water starts at the bottom of the bilge, UNDER the floor grating: a little water is seen through the bars
        private const float HalfBeam = CharacterMotion.HalfX;    // the water spans the whole interior width

        private MovingFrameModel _model;
        private readonly Transform[] _slabs = new Transform[Compartments];

        public void Bind(MovingFrameModel model, Transform boat)
        {
            _model = model;
            var water = SpikePrimitives.Water();
            for (int i = 0; i < Compartments; i++)
            {
                var go = SpikePrimitives.Create(PrimitiveType.Cube);
                go.name = "Water" + (i + 1);
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(boat, false);
                float length = BoatLayout.CompartmentZ1[i] - BoatLayout.CompartmentZ0[i];
                go.transform.localPosition = new Vector3(0f, 0f, 0.5f * (BoatLayout.CompartmentZ0[i] + BoatLayout.CompartmentZ1[i]));
                go.transform.localScale = new Vector3(2f * HalfBeam, 0.001f, length);
                var renderer = go.GetComponent<Renderer>();
                renderer.material = new Material(water) { color = new Color(0.04f, 0.18f, 0.26f, 0.5f) };
                _slabs[i] = go.transform;
                go.SetActive(false);
            }
        }

        private void Update()
        {
            if (_model == null) return;
            var water = _model.Water;
            for (int i = 0; i < Compartments; i++)
            {
                float fill = water.Valid && i < water.Fill.Length ? Mathf.Clamp01(water.Fill[i]) : 0f;
                bool visible = fill > 0.002f;
                if (_slabs[i].gameObject.activeSelf != visible) _slabs[i].gameObject.SetActive(visible);
                if (!visible) continue;
                float height = fill * Depth - BilgeFloorY;           // the surface is at fill x 2 m above the walkway; the slab reaches down to the bilge
                var s = _slabs[i].localScale; s.y = height; _slabs[i].localScale = s;
                var p = _slabs[i].localPosition; p.y = BilgeFloorY + height / 2f; _slabs[i].localPosition = p;
            }
        }
    }
}
