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

        public void Bind(MovingFrameModel model, IClockService clock, BoatMotion motion)
        {
            _model = model; _clock = clock; _motion = motion;
        }

        private void LateUpdate()
        {
            if (_model == null || !_model.HasServerTime) return;
            var water = _model.Water;   // trim / list added by the weight of the water (zero until the server sends it)
            var pose = _motion.Evaluate(_model.EstimateServerTime(_clock.Now), water.Valid ? water.TrimDeg : 0f, water.Valid ? water.ListDeg : 0f);
            transform.SetPositionAndRotation(
                new Vector3(pose.Position.X, pose.Position.Y, pose.Position.Z),
                new Quaternion(pose.Rotation.X, pose.Rotation.Y, pose.Rotation.Z, pose.Rotation.W));
        }
    }
}
