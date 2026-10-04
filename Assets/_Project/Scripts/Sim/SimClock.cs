namespace SousTension.Sim
{
    /// <summary>Fixed-step simulation clock (10 Hz, deterministic). No UnityEngine dependency.</summary>
    public sealed class SimClock
    {
        public const int TickRateHz = 10;
        public const float TickSeconds = 1f / TickRateHz;

        public int Tick { get; private set; }

        public void Advance() => Tick++;
    }
}
