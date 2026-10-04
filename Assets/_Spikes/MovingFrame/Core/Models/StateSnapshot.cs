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

    /// <summary>One authoritative broadcast from the match (10 Hz).</summary>
    public sealed class StateSnapshot
    {
        public readonly int Tick;
        public readonly double ServerTime; // seconds = tick * 0.1
        public readonly PlayerState[] Players;

        public StateSnapshot(int tick, double serverTime, PlayerState[] players)
        {
            Tick = tick; ServerTime = serverTime; Players = players;
        }
    }
}
