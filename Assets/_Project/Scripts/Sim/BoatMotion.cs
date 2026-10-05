using System;
using System.Numerics;

namespace SousTension.Sim
{
    /// <summary>Pose of the boat in world space (rigid transform).</summary>
    public readonly struct BoatPose
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;

        public BoatPose(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }

        /// <summary>World-space point to boat-local space.</summary>
        public Vector3 ToLocal(Vector3 world)
            => Vector3.Transform(world - Position, Quaternion.Inverse(Rotation));

        /// <summary>Boat-local point to world space.</summary>
        public Vector3 ToWorld(Vector3 local)
            => Vector3.Transform(local, Rotation) + Position;
    }

    /// <summary>
    /// Deterministic boat motion as a pure function of time (seconds).
    /// Used by the moving-frame spike (E1-01): every peer computes the same pose from the same
    /// synchronized time, so only the characters' boat-local positions need replication.
    /// No UnityEngine dependency.
    /// </summary>
    public sealed class BoatMotion
    {
        public float PitchAmplitudeDeg { get; }
        public float RollAmplitudeDeg { get; }
        public float HeaveAmplitude { get; }
        public float SurgeAmplitude { get; }
        public float PitchPeriod { get; }
        public float RollPeriod { get; }
        public float HeavePeriod { get; }
        public float SurgePeriod { get; }

        public BoatMotion(
            float pitchAmplitudeDeg = 15f, float rollAmplitudeDeg = 20f,
            float heaveAmplitude = 1.0f, float surgeAmplitude = 2.0f,
            float pitchPeriod = 7f, float rollPeriod = 5f,
            float heavePeriod = 6f, float surgePeriod = 9f)
        {
            PitchAmplitudeDeg = pitchAmplitudeDeg;
            RollAmplitudeDeg = rollAmplitudeDeg;
            HeaveAmplitude = heaveAmplitude;
            SurgeAmplitude = surgeAmplitude;
            PitchPeriod = pitchPeriod;
            RollPeriod = rollPeriod;
            HeavePeriod = heavePeriod;
            SurgePeriod = surgePeriod;
        }

        public BoatPose Evaluate(double timeSeconds) => Evaluate(timeSeconds, 0f, 0f);

        /// <summary>
        /// Pose with the trim (pitch, + = bow down) and list (roll) that the water's weight adds to the scripted swell (E6-01). The offsets are
        /// authoritative (server/src/water.js) and replicated; with both at zero this is the plain swell.
        /// </summary>
        public BoatPose Evaluate(double timeSeconds, float trimOffsetDeg, float listOffsetDeg)
        {
            double tau = 2.0 * Math.PI;
            float pitch = DegToRad(PitchAmplitudeDeg) * (float)Math.Sin(tau * timeSeconds / PitchPeriod) + DegToRad(trimOffsetDeg);
            float roll = DegToRad(RollAmplitudeDeg) * (float)Math.Sin(tau * timeSeconds / RollPeriod + 1.0) + DegToRad(listOffsetDeg);
            float heave = HeaveAmplitude * (float)Math.Sin(tau * timeSeconds / HeavePeriod + 2.0);
            float surge = SurgeAmplitude * (float)Math.Sin(tau * timeSeconds / SurgePeriod + 3.0);

            // Unity convention: x = right, y = up, z = forward. Pitch about X, roll about Z.
            var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, pitch)
                         * Quaternion.CreateFromAxisAngle(Vector3.UnitZ, roll);
            return new BoatPose(new Vector3(0f, heave, surge), Quaternion.Normalize(rotation));
        }

        private static float DegToRad(float deg) => deg * (float)(Math.PI / 180.0);
    }
}
