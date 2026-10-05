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

        /// <summary>True when the local player carries a hull patch or the bucket (E2-02: a click with no aimed target then uses the item).</summary>
        public bool IsLocalHoldingUsableItem()
        {
            foreach (var c in _cargoLatest.Values)
            {
                if (c.Kind != "patch" && c.Kind != "bucket") continue;
                foreach (var id in c.Carriers) if (id == LocalId) return true;
            }
            return false;
        }

        private readonly Dictionary<string, string[]> _hands = new Dictionary<string, string[]>();

        /// <summary>What a player holds, [left, right, pocket] as cargo ids ("" = empty); all empty for an unknown player (E2-03).</summary>
        public string[] HandsOf(string playerId) => _hands.TryGetValue(playerId ?? "", out var h) ? h : new[] { "", "", "" };

        /// <summary>Walking speed factor from what the local player carries (E2-04, same rule as the server: CarryLoad).</summary>
        public float LocalCarryFactor()
        {
            if (string.IsNullOrEmpty(LocalId)) return 1f;
            var held = HandsOf(LocalId);
            float mass = 0f;
            for (int i = 0; i < held.Length; i++)
            {
                if (string.IsNullOrEmpty(held[i])) continue;
                bool seen = false;
                for (int k = 0; k < i; k++) if (held[k] == held[i]) seen = true;      // a two-handed item fills both hands: count it once
                if (seen) continue;
                string kind = "crate"; bool heavy = false;
                if (_cargoLatest.TryGetValue(held[i], out var c)) { kind = c.Kind; heavy = c.Heavy; }
                mass += SousTension.Sim.CarryLoad.ItemMass(kind) / (heavy ? 2f : 1f);
            }
            return SousTension.Sim.CarryLoad.SpeedFactor(mass);
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

        /// <summary>Open hull leaks (E6-02), authoritative; a snapshot without the list keeps the last known one.</summary>
        public LeakState[] Leaks { get; private set; } = new LeakState[0];
        public void SetLeaks(LeakState[] leaks) { if (leaks != null) Leaks = leaks; }

        /// <summary>Bilge pumps (E6-03), authoritative; a snapshot without them keeps the last known state.</summary>
        public BilgeState Bilge { get; private set; }
        public void SetBilge(BilgeState state) { if (state.Valid) Bilge = state; }

        /// <summary>Electrical network (E3-07), authoritative; a snapshot without it keeps the last known state.</summary>
        public GridState Grid { get; private set; }
        public void SetGrid(GridState state) { if (state.Valid) Grid = state; }

        /// <summary>Propulsion and telegraph (E3-08), authoritative; a snapshot without it keeps the last known state.</summary>
        public PropulsionState Propulsion { get; private set; }
        public void SetPropulsion(PropulsionState state) { if (state.Valid) Propulsion = state; }

        /// <summary>The Operating Manual's page (E5-02), authoritative; a snapshot without it keeps the last known one.</summary>
        public ManualState Manual { get; private set; }
        public void SetManual(ManualState state) { if (state.Valid) Manual = state; }

        /// <summary>True while the local player holds the Operating Manual (it is read with both hands).</summary>
        public bool IsLocalReading()
        {
            if (string.IsNullOrEmpty(LocalId)) return false;
            foreach (var id in HandsOf(LocalId))
                if (!string.IsNullOrEmpty(id) && _cargoLatest.TryGetValue(id, out var c) && c.Kind == "manual") return true;
            return false;
        }

        public void SetLocal(float x, float z) { LocalX = x; LocalZ = z; }

        public void ApplyServerState(StateSnapshot snapshot, double localNow)
        {
            HasServerTime = true;
            LastServerTime = snapshot.ServerTime;
            LastReceiveLocalTime = localNow;
            var present = new HashSet<string>();
            foreach (var p in snapshot.Players)
            {
                _hands[p.Id] = p.Hands;
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
