using UnityEngine;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>Spike-only debug overlay with the E1-01 measurements (not the game's HUD: the game has none).</summary>
    public sealed class MetricsHudView : MonoBehaviour
    {
        private MovingFrameModel _model;
        private INetworkService _net;
        private MovingFrameController _controller;
        private IClockService _clock;
        private string _status = "connecting...";
        private long _lastSent, _lastRecv;
        private double _lastSample;
        private float _kbSent, _kbRecv;

        public void Bind(MovingFrameModel model, INetworkService net, MovingFrameController controller, IClockService clock)
        {
            _model = model; _net = net; _controller = controller; _clock = clock;
        }

        public void SetStatus(string status) => _status = status;

        private void OnGUI()
        {
            if (_model == null) return;
            double now = _clock.Now;
            if (now - _lastSample >= 1.0)
            {
                _kbSent = (_net.BytesSent - _lastSent) / 1024f; _kbRecv = (_net.BytesReceived - _lastRecv) / 1024f;
                _lastSent = _net.BytesSent; _lastRecv = _net.BytesReceived; _lastSample = now;
            }
            var il = _model.Interlock;
            GUI.Label(new Rect(10, 10, 560, 140),
                $"E1-01 moving frame | {_status}\n" +
                $"ack latency: {_model.LastAckLatencyMs:F0} ms | pending inputs: {_controller.PendingInputs}\n" +
                $"correction: last {_model.LastCorrection * 100f:F1} cm, max {_model.MaxCorrection * 100f:F1} cm\n" +
                $"bandwidth: up {_kbSent:F2} kB/s, down {_kbRecv:F2} kB/s | players: {_model.RemoteCountPlusLocal()}\n" +
                $"interlock: A {il.RemainingA / 10f:F1}s B {il.RemainingB / 10f:F1}s | last: {il.Result} | successes: {il.Count}");
        }
    }

    internal static class ModelExtensions
    {
        public static int RemoteCountPlusLocal(this MovingFrameModel m)
        {
            int n = 1; foreach (var _ in m.RemoteIds) n++; return n;
        }
    }
}
