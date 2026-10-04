using System;
using System.Collections.Generic;

namespace SousTension.Sim
{
    /// <summary>
    /// Client-side prediction with server reconciliation for one local character (boat-local space).
    /// Predict() applies an input immediately and remembers it; Reconcile() snaps to the authoritative
    /// state acknowledged up to <c>ackedSeq</c> and replays the inputs the server has not processed yet.
    /// </summary>
    public sealed class PredictionBuffer
    {
        private struct Pending { public int Seq; public float Mx, Mz; }

        private readonly List<Pending> _pending = new List<Pending>();
        private int _nextSeq = 1;

        public float X { get; private set; }
        public float Z { get; private set; }
        public int PendingCount => _pending.Count;

        public void Reset(float x, float z)
        {
            X = x; Z = z; _pending.Clear();
        }

        /// <summary>Apply one input tick locally and return its sequence number (to send to the server).</summary>
        public int Predict(float mx, float mz)
        {
            int seq = _nextSeq++;
            float x = X, z = Z;
            CharacterMotion.Step(ref x, ref z, mx, mz);
            X = x; Z = z;
            _pending.Add(new Pending { Seq = seq, Mx = mx, Mz = mz });
            return seq;
        }

        /// <returns>Distance (m) between the previous predicted position and the corrected one.</returns>
        public float Reconcile(float serverX, float serverZ, int ackedSeq)
        {
            float oldX = X, oldZ = Z;
            _pending.RemoveAll(p => p.Seq <= ackedSeq);
            float x = serverX, z = serverZ;
            foreach (var p in _pending) CharacterMotion.Step(ref x, ref z, p.Mx, p.Mz);
            X = x; Z = z;
            float dx = X - oldX, dz = Z - oldZ;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
