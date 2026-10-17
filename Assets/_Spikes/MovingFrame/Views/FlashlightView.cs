using UnityEngine;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// The torch (E11-03, GDD 3.3: the restart "à la lampe torche"): while the local player holds the flashlight (hand or chest pocket) a warm spot light
    /// shines from the camera along the line of sight. Diegetic: the only "interface" is the light itself. Other players' torches are not drawn yet.
    /// </summary>
    public sealed class FlashlightView : MonoBehaviour
    {
        private MovingFrameModel _model;
        private Light _beam;

        public void Bind(MovingFrameModel model, Camera camera)
        {
            _model = model;
            var go = new GameObject("FlashlightBeam");
            go.transform.SetParent(camera.transform, false);
            go.transform.localPosition = new Vector3(0.15f, -0.15f, 0.2f);                   // from the hand side, not from the eye
            _beam = go.AddComponent<Light>();
            _beam.type = LightType.Spot;
            _beam.range = 14f; _beam.spotAngle = 50f; _beam.innerSpotAngle = 25f;
            _beam.intensity = 8f; _beam.color = new Color(1f, 0.93f, 0.8f);
            _beam.shadows = LightShadows.None;
            _beam.enabled = false;
        }

        private void Update()
        {
            if (_model == null || _beam == null || string.IsNullOrEmpty(_model.LocalId)) return;
            bool on = false;
            foreach (var id in _model.HandsOf(_model.LocalId))
            {
                if (string.IsNullOrEmpty(id)) continue;
                foreach (var c in _model.Cargo) if (c.Id == id && c.Kind == "flashlight") on = true;
            }
            if (_beam.enabled != on) _beam.enabled = on;
        }
    }
}
