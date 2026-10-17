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
        public const float HalfZ = 14.0f;    // boat interior half length (m): the 28 m of the 3D boat

        /// <param name="speedFactor">1 = empty hands; what the character carries slows them down (<see cref="CarryLoad"/>, E2-04)</param>
        /// <param name="doorsOpen">hatch states (null: all open): bulkheads, baffles and closed hatches block the player (<see cref="BoatLayout"/>, E10-01)</param>
        public static void Step(ref float x, ref float z, float mx, float mz, float speedFactor = 1f, bool[] doorsOpen = null)
        {
            float len = (float)Math.Sqrt(mx * mx + mz * mz);
            if (len > 1f) { mx /= len; mz /= len; }
            float nx = Clamp(x + mx * MoveSpeed * speedFactor * SimClock.TickSeconds, -HalfX + BoatLayout.PlayerRadius, HalfX - BoatLayout.PlayerRadius);
            float nz = Clamp(z + mz * MoveSpeed * speedFactor * SimClock.TickSeconds, -HalfZ + BoatLayout.PlayerRadius, HalfZ - BoatLayout.PlayerRadius);
            if (BoatLayout.Blocked(x, z, doorsOpen)) { x = nx; z = nz; return; }      // already inside a wall (teleport): walk out freely
            if (!BoatLayout.Blocked(nx, z, doorsOpen)) x = nx;                       // axis by axis: a wall stops one component, the player slides
            if (!BoatLayout.Blocked(x, nz, doorsOpen)) z = nz;
        }

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}
