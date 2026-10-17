using System;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>What a key of the Rule of Two Players looks like (E4-03): grey when idle, a lit key whose timer runs down while it waits for the other
    /// player, a green flash on success and a red flash on failure. Diegetic: lamps and a shrinking light, never a HUD.</summary>
    public enum KeyLook { Idle, Armed, Success, Failure }

    public readonly struct KeyFeedback
    {
        public readonly KeyLook Look;
        /// <summary>Armed: share of the 3 s window still left (1 = just pressed, 0 = about to expire). Success / Failure: how much of the flash is left (1 to 0).</summary>
        public readonly float Fraction;
        public KeyFeedback(KeyLook look, float fraction) { Look = look; Fraction = fraction; }
    }

    /// <summary>Pure rules behind the station lamps (testable without Unity).</summary>
    public static class StationFeedback
    {
        public const int WindowTicks = 30;          // the official window: 3 s at 10 Hz (server INTERLOCK_WINDOW_TICKS)
        public const float FlashSeconds = 1.0f;

        /// <param name="remainingTicks">ticks left of the official window for this key (0 = idle, or in the server's latency grace)</param>
        /// <param name="holder">id of the player who pressed the key, "" when nobody</param>
        /// <param name="lastResult">outcome of the latest attempt on the whole action: "none", "success" or "timeout"</param>
        /// <param name="secondsSinceResult">how long ago that outcome happened (a large number when never)</param>
        public static KeyFeedback Compute(int remainingTicks, string holder, string lastResult, float secondsSinceResult)
        {
            if (lastResult != "none" && secondsSinceResult >= 0f && secondsSinceResult < FlashSeconds)
                return new KeyFeedback(lastResult == "success" ? KeyLook.Success : KeyLook.Failure, 1f - secondsSinceResult / FlashSeconds);
            bool armed = remainingTicks > 0 || !string.IsNullOrEmpty(holder);
            if (!armed) return new KeyFeedback(KeyLook.Idle, 0f);
            float f = remainingTicks / (float)WindowTicks;
            return new KeyFeedback(KeyLook.Armed, f < 0f ? 0f : (f > 1f ? 1f : f));
        }
    }
}
