using System;

namespace SousTension.Sim
{
    /// <summary>
    /// Fixed-step character motion in BOAT-LOCAL space (x = right, z = forward).
    /// Must stay identical to stepPlayer() in server/modules/index.js (the authoritative implementation).
    /// </summary>
    public static class CharacterMotion
    {
        public const float MoveSpeed = 3.0f; // m/s
        public const float HalfX = 3.0f;     // boat interior half width (m)
        public const float HalfZ = 10.0f;    // boat interior half length (m)

        /// <param name="speedFactor">1 = empty hands; what the character carries slows them down (<see cref="CarryLoad"/>, E2-04)</param>
        public static void Step(ref float x, ref float z, float mx, float mz, float speedFactor = 1f)
        {
            float len = (float)Math.Sqrt(mx * mx + mz * mz);
            if (len > 1f) { mx /= len; mz /= len; }
            x = Clamp(x + mx * MoveSpeed * speedFactor * SimClock.TickSeconds, -HalfX, HalfX);
            z = Clamp(z + mz * MoveSpeed * speedFactor * SimClock.TickSeconds, -HalfZ, HalfZ);
        }

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}
