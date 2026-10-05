using UnityEngine;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// Passive view of the water in the boat (E6-01): one translucent slab per compartment whose height follows the authoritative fill
    /// fraction (a full compartment is 2 m deep). Compartment layout mirrors WATER_COMPARTMENTS in server/src/water.js (six compartments of
    /// 20/6 m from the bow, +z, to the stern). The boat's trim and list are applied by BoatView. Built from primitives for the spike.
    /// </summary>
    public sealed class WaterView : MonoBehaviour
    {
        private const int Compartments = 6;
        private const float Depth = 2f;
        private const float HalfBeam = 3f;                      // CharacterMotion.HalfX: the water spans the whole interior width
        private const float HalfLength = 10f;
        private const float Length = 2f * HalfLength / Compartments;

        private MovingFrameModel _model;
        private readonly Transform[] _slabs = new Transform[Compartments];

        public void Bind(MovingFrameModel model, Transform boat)
        {
            _model = model;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            for (int i = 0; i < Compartments; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Water" + (i + 1);
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(boat, false);
                float zCentre = HalfLength - Length * (i + 0.5f);
                go.transform.localPosition = new Vector3(0f, 0f, zCentre);
                go.transform.localScale = new Vector3(2f * HalfBeam, 0.001f, Length);
                var renderer = go.GetComponent<Renderer>();
                renderer.material = new Material(shader) { color = new Color(0.1f, 0.35f, 0.55f, 0.6f) };
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
                float height = fill * Depth;
                var s = _slabs[i].localScale; s.y = height; _slabs[i].localScale = s;
                var p = _slabs[i].localPosition; p.y = height / 2f; _slabs[i].localPosition = p;
            }
        }
    }
}
