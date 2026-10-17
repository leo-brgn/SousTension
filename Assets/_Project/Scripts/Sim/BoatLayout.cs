using System;

namespace SousTension.Sim
{
    /// <summary>
    /// Geometry of the 3D boat in BOAT-LOCAL space (x = right, z = forward, bow = +z): 28 m x 6 m, six compartments of 4/6/4/4/4/6 m
    /// (blender/kit_environment.py, boat_layout.json). Five bulkheads, each with ONE hatch 0.9 m wide staggered left / right and a privacy baffle
    /// 0.95 m behind it. Circle (player) against axis-aligned rectangles. Must stay identical to server/src/layout.js (E10-01); a test compares them.
    /// </summary>
    public static class BoatLayout
    {
        public const float PlayerRadius = 0.25f;
        public const float HatchHalf = 0.45f;          // half width of the opening
        public const float WallHalf = 0.07f;           // half thickness of a bulkhead
        public const float BaffleHalfX = 0.8f, BaffleHalfZ = 0.06f, BaffleOffset = 0.95f;

        /// <summary>Bulkhead d separates compartment d (fore) from d+1 (aft); 0 = torpedo room ... 5 = living quarters.</summary>
        public static readonly float[] BulkheadZ = { 10f, 4f, 0f, -4f, -8f };
        public static readonly float[] HatchX = { -1.45f, 1.45f, -1.45f, 1.45f, -1.45f };
        /// <summary>Compartment z range [z0, z1], bow first (the same rooms as WATER_COMPARTMENTS on the server).</summary>
        public static readonly float[] CompartmentZ0 = { 10f, 4f, 0f, -4f, -8f, -14f };
        public static readonly float[] CompartmentZ1 = { 14f, 10f, 4f, 0f, -4f, -8f };
        public static int BulkheadCount => BulkheadZ.Length;

        // [x0, x1, z0, z1]: the bulkhead walls on each side of the hatch and the baffles.
        private static readonly float[][] Obstacles = BuildObstacles();

        private static float[][] BuildObstacles()
        {
            var list = new System.Collections.Generic.List<float[]>();
            for (int d = 0; d < BulkheadZ.Length; d++)
            {
                float z = BulkheadZ[d], cx = HatchX[d];
                list.Add(new[] { -CharacterMotion.HalfX, cx - HatchHalf, z - WallHalf, z + WallHalf });
                list.Add(new[] { cx + HatchHalf, CharacterMotion.HalfX, z - WallHalf, z + WallHalf });
                list.Add(new[] { cx - BaffleHalfX, cx + BaffleHalfX, z - BaffleOffset - BaffleHalfZ, z - BaffleOffset + BaffleHalfZ });
            }
            return list.ToArray();
        }

        public static int ObstacleCount => Obstacles.Length;
        /// <summary>One static obstacle as x0, x1, z0, z1 (the views build their blockout from the same numbers).</summary>
        public static void GetObstacle(int i, out float x0, out float x1, out float z0, out float z1)
        { var r = Obstacles[i]; x0 = r[0]; x1 = r[1]; z0 = r[2]; z1 = r[3]; }

        private static bool CircleHitsRect(float x, float z, float x0, float x1, float z0, float z1)
        {
            float cx = x < x0 ? x0 : (x > x1 ? x1 : x), cz = z < z0 ? z0 : (z > z1 ? z1 : z);
            float dx = x - cx, dz = z - cz;
            return dx * dx + dz * dz < PlayerRadius * PlayerRadius;
        }

        /// <summary>Is a player's circle at (x, z) inside a wall? <paramref name="doorsOpen"/> = hatch states (null: all open); a closed hatch is a wall too.</summary>
        public static bool Blocked(float x, float z, bool[] doorsOpen = null)
        {
            for (int i = 0; i < Obstacles.Length; i++)
            {
                var r = Obstacles[i];
                if (CircleHitsRect(x, z, r[0], r[1], r[2], r[3])) return true;
            }
            if (doorsOpen != null)
            {
                for (int d = 0; d < BulkheadZ.Length && d < doorsOpen.Length; d++)
                {
                    if (doorsOpen[d]) continue;
                    if (CircleHitsRect(x, z, HatchX[d] - HatchHalf, HatchX[d] + HatchHalf, BulkheadZ[d] - WallHalf, BulkheadZ[d] + WallHalf)) return true;
                }
            }
            return false;
        }

        /// <summary>The compartment (0 = bow) that contains z.</summary>
        public static int CompartmentAt(float z)
        {
            for (int i = 0; i < CompartmentZ0.Length; i++) if (z >= CompartmentZ0[i] && z < CompartmentZ1[i]) return i;
            return z < 0f ? CompartmentZ0.Length - 1 : 0;
        }
    }

    /// <summary>
    /// Where the controls and the coupled-action commands stand, in boat-local metres. Must stay identical to server/src/controls.js, bilge.js,
    /// power.js and coupled.js (a test compares them): the server decides who is within reach of what, the views only draw it.
    /// </summary>
    public static class ControlLayout
    {
        public const float RegimeX = -2.3f, RegimeZ = -2.0f;
        public const float ScramX = -2.7f, ScramZ = -0.8f;
        public const float ValveX = 2.6f;
        public static readonly float[] ValveZ = { -3.3f, -2.5f, -1.7f, -0.9f };
        public const float PumpX = 2.0f;
        public static readonly float[] PumpZ = { -2.9f, -1.1f };
        public static readonly float[] BilgeX = { 0.0f, 0.0f };
        public static readonly float[] BilgeZ = { -3.45f, -7.35f };
        public const float TeleX = -2.3f, TeleUpZ = 6.15f, TeleDownZ = 5.35f;

        /// <summary>The 20 breakers sit on the electrical panel of the machines compartment: 5 columns x 4 rows (row 0 = lowest, 0.54 m).</summary>
        public const int BreakerCount = 20, BreakerColumns = 5;
        public static readonly float[] BreakerHeight = { 0.54f, 0.77f, 1.0f, 1.23f };
        public static int BreakerRow(int i) => i / BreakerColumns;
        public static float BreakerX(int i) => -2.44f + 0.03f * BreakerRow(i);
        public static float BreakerZ(int i) => -6.0f + 0.2f * (i % BreakerColumns) + 0.03f * BreakerRow(i);

        /// <summary>Command positions (a, b) of the coupled actions, in the order of server COUPLED_ACTIONS.</summary>
        public static readonly (string id, float ax, float az, float bx, float bz)[] Coupled =
        {
            ("demo", 0f, -12f, 0f, 12f),
            ("demo2", -0.8f, 7.0f, 0.8f, -6.5f),
            ("reactor_restart", 1.2f, -3.6f, 1.2f, 8.0f),
        };
    }
}
