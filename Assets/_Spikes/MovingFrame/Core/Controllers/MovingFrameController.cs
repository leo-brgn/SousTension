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
        private readonly MovingFrameModel _model;
        private readonly PredictionBuffer _prediction = new PredictionBuffer();
        private readonly Dictionary<int, double> _sendTimes = new Dictionary<int, double>();

        private double _accumulator;
        private bool _initialized;
        private int _lastAcked;
        private bool _prevAct, _prevGrab;

        public MovingFrameController(INetworkService net, IInputSource input, IClockService clock, MovingFrameModel model)
        {
            _net = net; _input = input; _clock = clock; _model = model;
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
                int seq = _prediction.Predict(mx, mz);
                _sendTimes[seq] = _clock.Now;
                _net.SendInput(seq, mx, mz, act, grab, actHeld);   // hold = key still down: valve wheels turn while it is held
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
