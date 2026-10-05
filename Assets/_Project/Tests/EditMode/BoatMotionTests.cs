using System;
using System.Numerics;
using NUnit.Framework;
using SousTension.Sim;

namespace SousTension.Tests
{
    public class BoatMotionTests
    {
        private static float RollDeg(Quaternion q) => 0f; // unused placeholder to keep helpers explicit

        [Test]
        public void Evaluate_IsDeterministic()
        {
            var a = new BoatMotion().Evaluate(12.345);
            var b = new BoatMotion().Evaluate(12.345);
            Assert.AreEqual(a.Position, b.Position);
            Assert.AreEqual(a.Rotation, b.Rotation);
        }

        [Test]
        public void ToLocal_ToWorld_RoundTrip()
        {
            var motion = new BoatMotion();
            for (double t = 0; t < 30; t += 0.37)
            {
                var pose = motion.Evaluate(t);
                var local = new Vector3(1.5f, 0.0f, -3.0f);
                var back = pose.ToLocal(pose.ToWorld(local));
                Assert.That(Vector3.Distance(local, back), Is.LessThan(1e-4f), $"t={t}");
            }
        }

        [Test]
        public void Evaluate_StaysWithinConfiguredAmplitudes()
        {
            var motion = new BoatMotion(pitchAmplitudeDeg: 15f, rollAmplitudeDeg: 20f);
            // The boat's up axis must never tilt beyond sqrt(15^2 + 20^2) degrees (small-angle bound with margin).
            float maxTilt = (float)(Math.Sqrt(15 * 15 + 20 * 20) * Math.PI / 180.0) + 0.05f;
            for (double t = 0; t < 120; t += 0.05)
            {
                var up = Vector3.Transform(Vector3.UnitY, motion.Evaluate(t).Rotation);
                float tilt = (float)Math.Acos(Math.Min(1f, Vector3.Dot(up, Vector3.UnitY)));
                Assert.That(tilt, Is.LessThanOrEqualTo(maxTilt), $"t={t}");
            }
        }

        [Test]
        public void TiltProjection_MatchesServerGoldenValues()
        {
            // Horizontal part of world "up" in boat-local axes. The same golden values are asserted in
            // server/test/match.test.js against boatUpHorizontal() (the server uses it to slide loose cargo).
            var golden = new[]
            {
                (t: 0.0,   x: 0.289523314f,  z: 0.0f),
                (t: 1.5,   x: 0.0856205745f, z: -0.252473316f),
                (t: 3.7,   x: -0.204979999f, z: 0.0467290627f),
                (t: 10.25, x: 0.330693301f,  z: -0.0582228990f),
            };
            var motion = new BoatMotion();
            foreach (var g in golden)
            {
                var upLocal = Vector3.Transform(Vector3.UnitY, Quaternion.Inverse(motion.Evaluate(g.t).Rotation));
                Assert.AreEqual(g.x, upLocal.X, 1e-5f, $"t={g.t} x");
                Assert.AreEqual(g.z, upLocal.Z, 1e-5f, $"t={g.t} z");
            }
        }

        [Test]
        public void LocalPositionIsStable_WhileWorldPositionMoves()
        {
            // A character standing still in boat-local space must move in world space as the boat moves.
            var motion = new BoatMotion();
            var local = new Vector3(2f, 0f, 4f);
            var w0 = motion.Evaluate(0).ToWorld(local);
            var w1 = motion.Evaluate(2).ToWorld(local);
            Assert.That(Vector3.Distance(w0, w1), Is.GreaterThan(0.1f));
        }
    
        [Test]
        public void TrimAndListOffsets_AreAddedToTheSwell_AndZeroOffsetsChangeNothing()
        {
            var motion = new BoatMotion();
            Assert.AreEqual(motion.Evaluate(7.7).Rotation, motion.Evaluate(7.7, 0f, 0f).Rotation);
            // same formula as server/src/cargo.js boatUpHorizontal(t, trim, list): up_local = (cos p sin r, cos p cos r, -sin p)
            const double tau = 2.0 * System.Math.PI;
            double t = 7.7, trim = 5.0, list = -3.0;
            double p = 15.0 * System.Math.PI / 180.0 * System.Math.Sin(tau * t / 7.0) + trim * System.Math.PI / 180.0;
            double r = 20.0 * System.Math.PI / 180.0 * System.Math.Sin(tau * t / 5.0 + 1.0) + list * System.Math.PI / 180.0;
            var up = Vector3.Transform(Vector3.UnitY, Quaternion.Inverse(motion.Evaluate(t, (float)trim, (float)list).Rotation));
            Assert.AreEqual((float)(System.Math.Cos(p) * System.Math.Sin(r)), up.X, 1e-4f);
            Assert.AreEqual((float)(-System.Math.Sin(p)), up.Z, 1e-4f);
        }
}
}
