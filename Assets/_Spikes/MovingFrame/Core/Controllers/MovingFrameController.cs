using System;
using System.Collections.Generic;
using SousTension.Sim;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// Orchestrates the moving-frame scene: runs the fixed 10 Hz input tick, predicts the local character,
    /// sends inputs through <see cref="INetworkService"/>, and reconciles/stores authoritative snapshots in the model.
    /// Plain C# (no MonoBehaviour) so it can be tested in EditMode with fake services.
    /// </summary>
    public sealed class MovingFrameController : ITickable, IDisposable
    {
        private const int MaxCatchUpTicks = 5;

        private readonly INetworkService _net;
        private readonly IInputSource _input;
        private readonly IClockService _clock;
        private readonly IUseInput _use;
        private readonly IAimSource _aim;
        private readonly IHandsInput _hands;
        private readonly ILookSource _look;
        private readonly DebugConsoleModel _console;
        private readonly MovingFrameModel _model;
        private readonly PredictionBuffer _prediction = new PredictionBuffer();
        private readonly Dictionary<int, double> _sendTimes = new Dictionary<int, double>();

        private double _accumulator;
        private bool _initialized;
        private int _lastAcked;
        private bool _prevAct, _prevGrab, _prevUse, _prevTake, _prevDrop, _prevStow, _prevThrow, _prevNext, _prevPrev;

        /// <param name="use">primary action button (mouse); null = keyboard only (position-based interaction)</param>
        /// <param name="aim">what the player looks at; null = nothing aimable</param>
        public MovingFrameController(INetworkService net, IInputSource input, IClockService clock, MovingFrameModel model, IUseInput use = null, IAimSource aim = null, IHandsInput hands = null, ILookSource look = null, DebugConsoleModel console = null)
        {
            _net = net; _input = input; _clock = clock; _model = model; _use = use; _aim = aim; _hands = hands; _look = look; _console = console;
            if (_console != null) _net.DebugReceived += _console.AddReply;
            _net.StateReceived += OnState;
        }

        public int PendingInputs => _prediction.PendingCount;

        public void Tick(float deltaTime)
        {
            _net.Poll();
            if (!_initialized) return;

            _accumulator += deltaTime;
            int ticks = 0;
            while (_accumulator >= SimClock.TickSeconds && ticks < MaxCatchUpTicks)
            {
                _accumulator -= SimClock.TickSeconds;
                ticks++;
                _input.Read(out float mx, out float mz, out bool actHeld, out bool grabHeld);
                bool act = actHeld && !_prevAct;     // one activation per key press (edge), the server rejects repeats anyway
                bool grab = grabHeld && !_prevGrab;  // grab/drop toggles on each key press
                _prevAct = actHeld; _prevGrab = grabHeld;
                // Aimed interaction (E2-02): the primary button acts on the object looked at (press = act, held = hold); with a patch or the bucket in
                // hand and nothing aimed it uses the item. The interaction key E keeps the position-based behaviour.
                string use = null;
                bool useHeld = _use != null && _use.UseHeld;
                if (useHeld)
                {
                    use = _aim?.TargetId;
                    if (use == null && _model.IsLocalHoldingUsableItem()) use = "item";
                    if (use != null) { act |= !_prevUse; actHeld = true; }
                }
                _prevUse = useHeld;
                // Hands (E2-03): one command per tick, on the key press (edge): drop wins over stow, stow over take.
                string hand = null;
                if (_hands != null)
                {
                    bool take = _hands.TakeHeld, drop = _hands.DropHeld, stow = _hands.StowHeld, thr = _hands.ThrowHeld, next = _hands.NextHeld, prev = _hands.PrevHeld;
                    if (thr && !_prevThrow) hand = "throw"; else if (drop && !_prevDrop) hand = "drop"; else if (stow && !_prevStow) hand = "stow"; else if (take && !_prevTake) hand = "take";
                    else if (next && !_prevNext) hand = "next"; else if (prev && !_prevPrev) hand = "prev";    // turn the Manual's pages
                    _prevTake = take; _prevDrop = drop; _prevStow = stow; _prevThrow = thr; _prevNext = next; _prevPrev = prev;
                }
                int seq = _prediction.Predict(mx, mz, _model.LocalCarryFactor());
                _sendTimes[seq] = _clock.Now;
                _net.SendInput(seq, mx, mz, act, grab, actHeld, use, hand, _look != null ? _look.Yaw : 0f, _console?.TakeCommand());   // one debug command per tick   // hold = key still down: valve wheels turn while it is held
            }
            if (ticks == MaxCatchUpTicks) _accumulator = 0; // drop backlog after a long stall
            _model.SetLocal(_prediction.X, _prediction.Z);
        }

        private void OnState(StateSnapshot snapshot)
        {
            _model.LocalId = _net.LocalUserId;
            _model.ApplyServerState(snapshot, _clock.Now);
            _model.SetInterlock(snapshot.Interlock);
            _model.SetCoupled(snapshot.Coupled);
            _model.ApplyCargo(snapshot.Cargo, snapshot.ServerTime);
            _model.SetReactor(snapshot.Reactor);
            _model.SetLever(snapshot.Lever);
            _model.SetBoat(snapshot.Boat);
            _model.SetRestart(snapshot.Restart);
            _model.SetWater(snapshot.Water);
            _model.SetLeaks(snapshot.Leaks);
            _model.SetBilge(snapshot.Bilge);
            _model.SetGrid(snapshot.Grid);
            _model.SetPropulsion(snapshot.Propulsion);
            _model.SetManual(snapshot.Manual);

            foreach (var p in snapshot.Players)
            {
                if (p.Id != _net.LocalUserId) continue;
                if (!_initialized)
                {
                    _prediction.Reset(p.X, p.Z);
                    _initialized = true;
                }
                else
                {
                    float correction = _prediction.Reconcile(p.X, p.Z, p.Seq);
                    _model.LastCorrection = correction;
                    if (correction > _model.MaxCorrection) _model.MaxCorrection = correction;
                }
                RecordAckLatency(p.Seq);
                _model.SetLocal(_prediction.X, _prediction.Z);
                break;
            }
        }

        private void RecordAckLatency(int ackedSeq)
        {
            if (ackedSeq <= _lastAcked) return;
            if (_sendTimes.TryGetValue(ackedSeq, out var sentAt))
                _model.LastAckLatencyMs = (_clock.Now - sentAt) * 1000.0;
            for (int s = _lastAcked + 1; s <= ackedSeq; s++) _sendTimes.Remove(s);
            _lastAcked = ackedSeq;
        }

        public void Dispose() => _net.StateReceived -= OnState;
    }
}
