using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// Decorator that adds one-way latency, jitter and "packet loss" to any <see cref="INetworkService"/>.
    /// Nakama's realtime socket is a WebSocket (TCP): a lost packet is not dropped, it is retransmitted
    /// after a timeout and blocks everything queued behind it (head-of-line blocking). So a loss event here
    /// adds <c>retransmitDelay</c> to that message, and delivery stays strictly in order per direction.
    /// </summary>
    public sealed class SimulatedLatencyNetworkService : INetworkService
    {
        private struct Delayed<T> { public double Due; public T Item; }

        private readonly INetworkService _inner;
        private readonly IClockService _clock;
        private readonly double _oneWayLatency;
        private readonly double _jitter;
        private readonly double _loss;
        private readonly double _retransmitDelay;
        private readonly Random _rng;
        private readonly List<Delayed<(int seq, float mx, float mz, bool act, bool grab, bool hold)>> _outgoing = new List<Delayed<(int, float, float, bool, bool, bool)>>();
        private readonly List<Delayed<StateSnapshot>> _incoming = new List<Delayed<StateSnapshot>>();
        private double _lastOutDue, _lastInDue;

        public event Action<StateSnapshot> StateReceived;

        public SimulatedLatencyNetworkService(INetworkService inner, IClockService clock,
            double oneWayLatencySeconds, double jitterSeconds = 0.0, double lossRate = 0.0,
            double retransmitDelaySeconds = 0.2, int seed = 1234)
        {
            _inner = inner; _clock = clock;
            _oneWayLatency = oneWayLatencySeconds; _jitter = jitterSeconds; _loss = lossRate;
            _retransmitDelay = retransmitDelaySeconds;
            _rng = new Random(seed);
            _inner.StateReceived += OnInnerState;
        }

        public string LocalUserId => _inner.LocalUserId;
        public long BytesSent => _inner.BytesSent;
        public long BytesReceived => _inner.BytesReceived;

        public Task ConnectAsync(CancellationToken ct) => _inner.ConnectAsync(ct);

        public void SendInput(int seq, float moveX, float moveZ, bool act, bool grab, bool hold = false)
        {
            _lastOutDue = Math.Max(_lastOutDue, _clock.Now + NextDelay());
            _outgoing.Add(new Delayed<(int, float, float, bool, bool, bool)> { Due = _lastOutDue, Item = (seq, moveX, moveZ, act, grab, hold) });
        }

        private void OnInnerState(StateSnapshot s)
        {
            _lastInDue = Math.Max(_lastInDue, _clock.Now + NextDelay());
            _incoming.Add(new Delayed<StateSnapshot> { Due = _lastInDue, Item = s });
        }

        public void Poll()
        {
            _inner.Poll();
            double now = _clock.Now;
            while (_outgoing.Count > 0 && _outgoing[0].Due <= now)
            {
                var o = _outgoing[0].Item; _outgoing.RemoveAt(0);
                _inner.SendInput(o.seq, o.mx, o.mz, o.act, o.grab, o.hold);
            }
            while (_incoming.Count > 0 && _incoming[0].Due <= now)
            {
                var s = _incoming[0].Item; _incoming.RemoveAt(0);
                StateReceived?.Invoke(s);
            }
        }

        private double NextDelay()
        {
            double d = _oneWayLatency + (_jitter > 0 ? (_rng.NextDouble() * 2 - 1) * _jitter : 0.0);
            if (_loss > 0 && _rng.NextDouble() < _loss) d += _retransmitDelay;
            return Math.Max(0.0, d);
        }

        public void Dispose()
        {
            _inner.StateReceived -= OnInnerState;
            _inner.Dispose();
        }
    }
}
