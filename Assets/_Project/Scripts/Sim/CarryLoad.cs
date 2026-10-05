namespace SousTension.Sim
{
    /// <summary>
    /// What a character carries slows them down (E2-04). Must stay identical to ITEM_KINDS / carrySpeedFactor() in server/src/hands.js (the
    /// authoritative implementation): the client's prediction uses the same factor, otherwise the player would be pulled back at every step.
    /// </summary>
    public static class CarryLoad
    {
        public const float SlowdownPerKg = 0.012f;
        public const float MinFactor = 0.5f;

        /// <summary>Mass in kg of one item kind ("crate" for an unknown kind, as on the server).</summary>
        public static float ItemMass(string kind)
        {
            switch (kind)
            {
                case "fuel": return 40f;
                case "patch": return 1f;
                case "bucket": return 2f;
                case "flashlight": return 0.5f;
                case "manual": return 8f;
                default: return 12f;          // crate
            }
        }

        /// <summary>Walking speed factor for a carried mass in kg (1 = empty hands, never below <see cref="MinFactor"/>).</summary>
        public static float SpeedFactor(float massKg)
        {
            float f = 1f - SlowdownPerKg * massKg;
            return f < MinFactor ? MinFactor : (f > 1f ? 1f : f);
        }
    }
}
