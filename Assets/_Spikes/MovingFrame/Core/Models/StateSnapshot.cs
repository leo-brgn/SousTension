namespace SousTension.Spikes.MovingFrame
{
    /// <summary>Authoritative state of one character, in boat-local space.</summary>
    public readonly struct PlayerState
    {
        public readonly string Id;
        public readonly float X, Z;
        public readonly int Seq; // last input sequence the server has processed for this player

        public PlayerState(string id, float x, float z, int seq)
        {
            Id = id; X = x; Z = z; Seq = seq;
        }
    }

    /// <summary>Authoritative state of the two-player interlock (Rule of Two Players).</summary>
    public readonly struct InterlockState
    {
        public readonly int RemainingA, RemainingB;   // ticks left in the window for each station (0 = idle)
        public readonly string HolderA, HolderB;      // user id that pressed the station, "" when idle
        public readonly string Result;                // "none" | "success" | "timeout" (last outcome)
        public readonly int ResultTick;
        public readonly int Count;                    // successful interlocks since the match started

        public InterlockState(int remainingA, int remainingB, string holderA, string holderB, string result, int resultTick, int count)
        {
            RemainingA = remainingA; RemainingB = remainingB; HolderA = holderA ?? ""; HolderB = holderB ?? "";
            Result = result ?? "none"; ResultTick = resultTick; Count = count;
        }
    }

    /// <summary>Authoritative state of one piece of cargo, in boat-local space.</summary>
    public readonly struct CargoState
    {
        public readonly string Id;
        public readonly float X, Z;
        public readonly bool Heavy;          // needs two carriers
        public readonly string[] Carriers;   // 0 = loose (slides), 1 = carried, 2 = heavy carried
        public readonly string Pending;      // first carrier of a heavy item, waiting for the second one

        public CargoState(string id, float x, float z, bool heavy, string[] carriers, string pending)
        {
            Id = id; X = x; Z = z; Heavy = heavy; Carriers = carriers ?? new string[0]; Pending = pending ?? "";
        }
    }

    /// <summary>Authoritative reactor gauges (server-simulated, E3-02). The client only displays them.</summary>
    public readonly struct ReactorState
    {
        public readonly bool Valid;          // false when the server did not send reactor data
        public readonly string Regime;       // "veille" | "croisiere" | "pleine" (selector position)
        public readonly float Rods;          // 0..1 real rod position (lags the selector)
        public readonly float Noise;         // 0..4 plant noise (follows the rods)
        public readonly float Power, Temp, Steam, Electricity, Supply, Flow;   // MWth, degC, bar, MWe, 0..1, 0..~1.2
        public readonly float[] Valves;      // 4 main primary valves, 0..1
        public readonly bool[] Pumps;        // 2 primary pumps
        public readonly bool Scram, AutoScram, Leak, Warn, Crit;

        public ReactorState(string regime, float rods, float noise, float power, float temp, float steam, float electricity,
            float supply, float flow, float[] valves, bool[] pumps, bool scram, bool autoScram, bool leak, bool warn, bool crit)
        {
            Valid = true; Regime = regime ?? "veille"; Rods = rods; Noise = noise; Power = power; Temp = temp; Steam = steam;
            Electricity = electricity; Supply = supply; Flow = flow; Valves = valves ?? new float[4]; Pumps = pumps ?? new bool[2];
            Scram = scram; AutoScram = autoScram; Leak = leak; Warn = warn; Crit = crit;
        }
    }

    /// <summary>One authoritative broadcast from the match (10 Hz).</summary>
    public sealed class StateSnapshot
    {
        public readonly int Tick;
        public readonly double ServerTime; // seconds = tick * 0.1
        public readonly PlayerState[] Players;
        public readonly InterlockState Interlock;
        public readonly CargoState[] Cargo;
        public readonly ReactorState Reactor;

        public StateSnapshot(int tick, double serverTime, PlayerState[] players, InterlockState interlock = default, CargoState[] cargo = null, ReactorState reactor = default)
        {
            Tick = tick; ServerTime = serverTime; Players = players; Interlock = interlock; Cargo = cargo ?? new CargoState[0]; Reactor = reactor;
        }
    }
}
