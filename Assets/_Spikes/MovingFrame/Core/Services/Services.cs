using System;
using System.Threading;
using System.Threading.Tasks;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>Realtime connection to the authoritative match. Implementations: Nakama, simulated-latency decorator, fakes.</summary>
    public interface INetworkService : IDisposable
    {
        string LocalUserId { get; }
        long BytesSent { get; }
        long BytesReceived { get; }

        /// <summary>Raised from <see cref="Poll"/> on the caller's (main) thread only.</summary>
        event Action<StateSnapshot> StateReceived;

        Task ConnectAsync(CancellationToken cancellationToken);
        void SendInput(int seq, float moveX, float moveZ, bool act, bool grab, bool hold = false);

        /// <summary>Deliver queued network events on the calling thread.</summary>
        void Poll();
    }

    /// <summary>Monotonic clock in seconds.</summary>
    public interface IClockService
    {
        double Now { get; }
    }

    /// <summary>Movement intent in boat-local axes (x = right, z = forward), each in [-1, 1], plus the held state of the interact key (act) and the grab/drop key (grab).</summary>
    public interface IInputSource
    {
        void Read(out float moveX, out float moveZ, out bool act, out bool grab);
    }

    /// <summary>Anything advanced once per frame by the composition root.</summary>
    public interface ITickable
    {
        void Tick(float deltaTime);
    }
}
