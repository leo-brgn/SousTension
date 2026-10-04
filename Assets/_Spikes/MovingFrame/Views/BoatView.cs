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
            var pose = _motion.Evaluate(_model.EstimateServerTime(_clock.Now));
            transform.SetPositionAndRotation(
                new Vector3(pose.Position.X, pose.Position.Y, pose.Position.Z),
                new Quaternion(pose.Rotation.X, pose.Rotation.Y, pose.Rotation.Z, pose.Rotation.W));
        }
    }
}
