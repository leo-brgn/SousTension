namespace SousTension.Spikes.MovingFrame
{
    /// <summary>Authoritative state of one character, in boat-local space.</summary>
    public readonly struct PlayerState
    {
        public readonly string Id;
        public readonly float X, Z;
        public readonly int Seq; // last input sequence the server has processed for this player
        public readonly string[] Hands; // what the player holds (E2-03): cargo ids of [left hand, right hand, pocket], "" = empty

        public PlayerState(string id, float x, float z, int seq, string[] hands = null)
        {
            Id = id; X = x; Z = z; Seq = seq; Hands = hands ?? new[] { "", "", "" };
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

    /// <summary>One coupled action of the Rule of Two Players (E4-02): its id and the state of its two commands.</summary>
    public readonly struct CoupledActionState
    {
        public readonly string Id;
        public readonly InterlockState State;
        public CoupledActionState(string id, InterlockState state) { Id = id ?? ""; State = state; }
    }

    /// <summary>Authoritative state of one piece of cargo, in boat-local space.</summary>
    public readonly struct CargoState
    {
        public readonly string Id;
        public readonly float X, Z;
        public readonly bool Heavy;          // needs two carriers
        public readonly string[] Carriers;   // 0 = loose (slides), 1 = carried, 2 = heavy carried
        public readonly string Pending;      // first carrier of a heavy item, waiting for the second one
        public readonly string Kind;         // "crate" | "patch" (hull patch, E6-02)
        public readonly bool Active;         // false for a used patch that has not yet respawned in the toolbox
        public readonly float Y;             // height above the floor (E2-04): > 0 while a thrown item is in the air

        public CargoState(string id, float x, float z, bool heavy, string[] carriers, string pending, string kind = "crate", bool active = true, float y = 0f)
        {
            Kind = kind ?? "crate"; Active = active; Y = y;
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
        public readonly bool[] Pumps;        // 2 primary pumps (running)
        public readonly bool[] PumpBroken;   // 2 primary pumps (broken: cannot be started)
        public readonly bool Scram, AutoScram, Leak, Warn, Crit;

        public ReactorState(string regime, float rods, float noise, float power, float temp, float steam, float electricity,
            float supply, float flow, float[] valves, bool[] pumps, bool scram, bool autoScram, bool leak, bool warn, bool crit,
            bool[] pumpBroken = null)
        {
            PumpBroken = pumpBroken ?? new bool[2];
            Valid = true; Regime = regime ?? "veille"; Rods = rods; Noise = noise; Power = power; Temp = temp; Steam = steam;
            Electricity = electricity; Supply = supply; Flow = flow; Valves = valves ?? new float[4]; Pumps = pumps ?? new bool[2];
            Scram = scram; AutoScram = autoScram; Leak = leak; Warn = warn; Crit = crit;
        }
    }

    /// <summary>SCRAM lever under its sealed cover (E3-04), as decided by the server. Pulled = the reactor SCRAM latch.</summary>
    public readonly struct ScramLeverState
    {
        public readonly bool Valid;
        public readonly bool CoverOpen;      // lifted (stays open while the lever is down)
        public readonly bool Pulled;
        public ScramLeverState(bool coverOpen, bool pulled) { Valid = true; CoverOpen = coverOpen; Pulled = pulled; }
    }

    /// <summary>Reactor restart procedure after a SCRAM (E3-05): the three preparation steps and the outcome of the latest attempt.</summary>
    public readonly struct RestartState
    {
        public readonly bool Valid;
        public readonly bool LeverBack, ValvesOpen, PumpsRunning;   // steps 1-3 (step 4 is the coupled action "reactor_restart")
        public readonly string Last;                                 // "none" | "success" | "refused"
        public readonly int LastTick, Count;
        public RestartState(bool leverBack, bool valvesOpen, bool pumpsRunning, string last, int lastTick, int count)
        { Valid = true; LeverBack = leverBack; ValvesOpen = valvesOpen; PumpsRunning = pumpsRunning; Last = last ?? "none"; LastTick = lastTick; Count = count; }
    }

    /// <summary>Water in the boat (E6-01): fill fraction of each compartment and the trim / list its weight adds to the swell.</summary>
    public readonly struct WaterState
    {
        public readonly bool Valid;
        public readonly float[] Fill;        // 0..1 per compartment, bow (index 0) to stern
        public readonly float MassTonnes;
        public readonly float TrimDeg, ListDeg;
        public readonly bool[] DoorOpen;     // bulkhead openings between neighbouring compartments
        public WaterState(float[] fill, float massTonnes, float trimDeg, float listDeg, bool[] doorOpen)
        { Valid = true; Fill = fill ?? new float[0]; MassTonnes = massTonnes; TrimDeg = trimDeg; ListDeg = listDeg; DoorOpen = doorOpen ?? new bool[0]; }
    }

    /// <summary>One open hull leak (E6-02): where it is on the wall, how big, and what kind.</summary>
    public readonly struct LeakState
    {
        public readonly int Id, Compartment;   // compartment 0 = bow
        public readonly float X, Z;            // boat-local position on the hull wall
        public readonly int Size;              // 1 small, 2 medium, 3 large
        public readonly string Type;           // "rivet" | "plate"
        public LeakState(int id, int compartment, float x, float z, int size, string type)
        { Id = id; Compartment = compartment; X = x; Z = z; Size = size; Type = type ?? "plate"; }
    }

    /// <summary>The two bilge pumps (E6-03): switch state and whether each is actually moving water.</summary>
    public readonly struct BilgeState
    {
        public readonly bool Valid;
        public readonly int[] State;         // per pump: 0 stopped, 1 on, 2 broken
        public readonly bool[] Running;      // per pump: moving water this tick (on, powered, something to pump)
        public BilgeState(int[] state, bool[] running) { Valid = true; State = state ?? new int[0]; Running = running ?? new bool[0]; }
    }

    /// <summary>The electrical network (E3-07): bus voltage, emergency battery, breakers and the light band of each compartment.</summary>
    public readonly struct GridState
    {
        public readonly bool Valid;
        public readonly float Voltage;       // 0..1 bus voltage
        public readonly float Battery;       // 0..1 emergency battery charge
        public readonly bool Emergency;      // emergency (red) lights on: the grid is dark and the battery is not empty
        public readonly int[] Breakers;      // per breaker: 0 open, 1 closed, 2 tripped
        public readonly int[] LightBands;    // per compartment: 3 white, 2 orange, 1 red, 0 dark
        public readonly float Demand, Surplus;   // MWe
        public GridState(float voltage, float battery, bool emergency, int[] breakers, int[] lightBands, float demand, float surplus)
        { Valid = true; Voltage = voltage; Battery = battery; Emergency = emergency; Breakers = breakers ?? new int[0]; LightBands = lightBands ?? new int[0]; Demand = demand; Surplus = surplus; }
    }

    /// <summary>Propulsion (E3-08): machine telegraph position, boat speed, distance travelled and propulsion noise.</summary>
    public readonly struct PropulsionState
    {
        public readonly bool Valid;
        public readonly int Telegraph;       // 0 arriere, 1 stop, 2 lent, 3 demi, 4 toute
        public readonly float Speed;         // m/s (negative astern)
        public readonly float Distance;      // m travelled (signed)
        public readonly float Noise;         // 0..4
        public PropulsionState(int telegraph, float speed, float distance, float noise)
        { Valid = true; Telegraph = telegraph; Speed = speed; Distance = distance; Noise = noise; }
    }

    /// <summary>Authoritative boat depth below patrol depth (E3-04): grows after a SCRAM. Display only.</summary>
    public readonly struct BoatDepthState
    {
        public readonly bool Valid;
        public readonly float Depth;         // m
        public readonly float DescentRate;   // m/s
        public BoatDepthState(float depth, float descentRate) { Valid = true; Depth = depth; DescentRate = descentRate; }
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
        public readonly ScramLeverState Lever;
        public readonly BoatDepthState Boat;
        public readonly CoupledActionState[] Coupled;
        public readonly RestartState Restart;
        public readonly WaterState Water;
        public readonly BilgeState Bilge;
        public readonly GridState Grid;
        public readonly PropulsionState Propulsion;
        public readonly LeakState[] Leaks;   // null = the server sent no leak list (older server); empty = no leak

        public StateSnapshot(int tick, double serverTime, PlayerState[] players, InterlockState interlock = default, CargoState[] cargo = null, ReactorState reactor = default,
            ScramLeverState lever = default, BoatDepthState boat = default, CoupledActionState[] coupled = null,
            RestartState restart = default, WaterState water = default, LeakState[] leaks = null, BilgeState bilge = default, GridState grid = default, PropulsionState propulsion = default)
        {
            Propulsion = propulsion;
            Grid = grid;
            Bilge = bilge;
            Leaks = leaks;
            Water = water;
            Restart = restart;
            Coupled = coupled ?? new CoupledActionState[0];
            Lever = lever; Boat = boat;
            Tick = tick; ServerTime = serverTime; Players = players; Interlock = interlock; Cargo = cargo ?? new CargoState[0]; Reactor = reactor;
        }
    }
}
