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

        // ---- Cargo (authoritative; interpolated like remote players, but locally-carried items use the prediction) ----
        private readonly Dictionary<string, List<Sample>> _cargoHistory = new Dictionary<string, List<Sample>>();
        private readonly Dictionary<string, CargoState> _cargoLatest = new Dictionary<string, CargoState>();

        public IEnumerable<CargoState> Cargo => _cargoLatest.Values;

        public void ApplyCargo(CargoState[] cargo, double serverTime)
        {
            foreach (var c in cargo)
            {
                _cargoLatest[c.Id] = c;
                if (!_cargoHistory.TryGetValue(c.Id, out var list)) { list = new List<Sample>(); _cargoHistory[c.Id] = list; }
                list.Add(new Sample { Time = serverTime, X = c.X, Z = c.Z });
                if (list.Count > MaxSamples) list.RemoveAt(0);
            }
        }

        public bool IsCarriedByLocal(string cargoId)
        {
            if (!_cargoLatest.TryGetValue(cargoId, out var c)) return false;
            foreach (var id in c.Carriers) if (id == LocalId) return true;
            return false;
        }

        /// <summary>
        /// Where to draw a piece of cargo. Carried by the local player: the predicted position (no visible lag between hands
        /// and cargo). Heavy item shared with the local player: midpoint of the local prediction and the partner at render time.
        /// Otherwise: authoritative history interpolated at <paramref name="renderServerTime"/>.
        /// </summary>
        public bool TryGetCargoRenderPosition(string id, double renderServerTime, out float x, out float z)
        {
            x = z = 0f;
            if (!_cargoLatest.TryGetValue(id, out var c)) return false;
            if (IsCarriedByLocal(id))
            {
                if (c.Carriers.Length == 1) { x = LocalX; z = LocalZ; return true; }
                string partner = c.Carriers[0] == LocalId ? c.Carriers[1] : c.Carriers[0];
                if (TrySampleRemote(partner, renderServerTime, out float px, out float pz)) { x = (LocalX + px) * 0.5f; z = (LocalZ + pz) * 0.5f; return true; }
            }
            if (!_cargoHistory.TryGetValue(id, out var s) || s.Count == 0) { x = c.X; z = c.Z; return true; }
            return Interpolate(s, renderServerTime, out x, out z);
        }

        private static bool Interpolate(List<Sample> s, double serverTime, out float x, out float z)
        {
            x = z = 0f;
            if (s.Count == 0) return false;
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

        /// <summary>Latest authoritative interlock state (never predicted: the server decides).</summary>
        public InterlockState Interlock { get; private set; }
        public void SetInterlock(InterlockState state) { Interlock = state; }

        /// <summary>Latest authoritative state of every coupled action (E4-02). Interlock above is the first one (legacy alias).</summary>
        public CoupledActionState[] Coupled { get; private set; } = new CoupledActionState[0];
        public void SetCoupled(CoupledActionState[] actions) { if (actions != null && actions.Length > 0) Coupled = actions; }
        public bool TryGetCoupled(string id, out InterlockState state)
        {
            foreach (var a in Coupled) if (a.Id == id) { state = a.State; return true; }
            state = default; return false;
        }

        /// <summary>Latest authoritative reactor gauges (Valid = false until the server has sent them).</summary>
        public ReactorState Reactor { get; private set; }
        public void SetReactor(ReactorState state) { if (state.Valid) Reactor = state; }

        /// <summary>Latest SCRAM lever (cover/pulled) and boat depth, authoritative (E3-04). Missing data keeps the last known values.</summary>
        public ScramLeverState Lever { get; private set; }
        public BoatDepthState Boat { get; private set; }
        public void SetLever(ScramLeverState state) { if (state.Valid) Lever = state; }
        public void SetBoat(BoatDepthState state) { if (state.Valid) Boat = state; }

        /// <summary>Latest reactor restart procedure state (E3-05), authoritative.</summary>
        public RestartState Restart { get; private set; }
        public void SetRestart(RestartState state) { if (state.Valid) Restart = state; }

        /// <summary>Latest authoritative water state (E6-01); the boat view adds its trim / list to the swell.</summary>
        public WaterState Water { get; private set; }
        public void SetWater(WaterState state) { if (state.Valid) Water = state; }

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
