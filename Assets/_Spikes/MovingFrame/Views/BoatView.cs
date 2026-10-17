using SousTension.Sim;
using UnityEngine;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>Passive view: moves the boat root to the deterministic pose at the estimated server time.</summary>
    public sealed class BoatView : MonoBehaviour
    {
        private MovingFrameModel _model;
        private IClockService _clock;
        private BoatMotion _motion;
        private float _trim, _list;                       // the water's tilt as drawn: it arrives in 10 Hz steps and is eased so the horizon never jumps
        private const float TiltEaseSeconds = 0.8f;

        public void Bind(MovingFrameModel model, IClockService clock, BoatMotion motion)
        {
            _model = model; _clock = clock; _motion = motion;
        }

        private void LateUpdate()
        {
            if (_model == null || !_model.HasServerTime) return;
            var water = _model.Water;   // trim / list added by the weight of the water (zero until the server sends it)
            float k = 1f - Mathf.Exp(-Time.deltaTime / TiltEaseSeconds);
            _trim = Mathf.Lerp(_trim, water.Valid ? water.TrimDeg : 0f, k);
            _list = Mathf.Lerp(_list, water.Valid ? water.ListDeg : 0f, k);
            var pose = _motion.Evaluate(_model.EstimateServerTime(_clock.Now), _trim, _list);
            transform.SetPositionAndRotation(
                new Vector3(pose.Position.X, pose.Position.Y, pose.Position.Z),
                new Quaternion(pose.Rotation.X, pose.Rotation.Y, pose.Rotation.Z, pose.Rotation.W));
        }
    }
}
