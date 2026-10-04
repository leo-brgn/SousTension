using System;
using System.Collections.Generic;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// State of the moving-frame scene: predicted local position, interpolation history of remote players
    /// and the estimated server clock (drives the deterministic boat motion). Pure C#, no engine types.
    /// </summary>
    public sealed class MovingFrameModel
    {
        private struct Sample { public double Time; public float X, Z; }

        private const int MaxSamples = 4;
        private readonly Dictionary<string, List<Sample>> _remotes = new Dictionary<string, List<Sample>>();

        public string LocalId { get; set; }
        public float LocalX { get; private set; }
        public float LocalZ { get; private set; }
        public bool HasServerTime { get; private set; }
        public double LastServerTime { get; private set; }
        public double LastReceiveLocalTime { get; private set; }

        // Metrics shown by the spike overlay
        public float LastCorrection { get; set; }
        public float MaxCorrection { get; set; }
        public double LastAckLatencyMs { get; set; }

        public IEnumerable<string> RemoteIds => _remotes.Keys;

        public void SetLocal(float x, float z) { LocalX = x; LocalZ = z; }

        public void ApplyServerState(StateSnapshot snapshot, double localNow)
        {
            HasServerTime = true;
            LastServerTime = snapshot.ServerTime;
            LastReceiveLocalTime = localNow;
            var present = new HashSet<string>();
            foreach (var p in snapshot.Players)
            {
                if (p.Id == LocalId) continue;
                present.Add(p.Id);
                if (!_remotes.TryGetValue(p.Id, out var list)) { list = new List<Sample>(); _remotes[p.Id] = list; }
                list.Add(new Sample { Time = snapshot.ServerTime, X = p.X, Z = p.Z });
                if (list.Count > MaxSamples) list.RemoveAt(0);
            }
            var gone = new List<string>();
            foreach (var id in _remotes.Keys) if (!present.Contains(id)) gone.Add(id);
            foreach (var id in gone) _remotes.Remove(id);
        }

        /// <summary>Server clock extrapolated from the last snapshot.</summary>
        public double EstimateServerTime(double localNow)
            => HasServerTime ? LastServerTime + (localNow - LastReceiveLocalTime) : 0.0;

        /// <summary>Interpolated position of a remote player at <paramref name="serverTime"/> (clamped to the known samples).</summary>
        public bool TrySampleRemote(string id, double serverTime, out float x, out float z)
        {
            x = z = 0f;
            if (!_remotes.TryGetValue(id, out var s) || s.Count == 0) return false;
            if (serverTime <= s[0].Time) { x = s[0].X; z = s[0].Z; return true; }
            for (int i = 0; i < s.Count - 1; i++)
            {
                var a = s[i]; var b = s[i + 1];
                if (serverTime >= a.Time && serverTime <= b.Time)
                {
                    float t = (float)((serverTime - a.Time) / Math.Max(1e-6, b.Time - a.Time));
                    x = a.X + (b.X - a.X) * t; z = a.Z + (b.Z - a.Z) * t;
                    return true;
                }
            }
            var last = s[s.Count - 1]; x = last.X; z = last.Z; return true;
        }
    }
}
