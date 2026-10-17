using System.Collections.Generic;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// The alarms of the boat, in the order of server/src/alarms.js (ALARMS): the annunciator draws one lamp for each. Must stay equal to the server's
    /// catalogue (a test compares the ids, the systems and the severities). The plate of a lamp is a code (system letter + number), not a text:
    /// the meaning is in the Operating Manual.
    /// </summary>
    public static class AlarmCatalog
    {
        public readonly struct Entry
        {
            public readonly string Id, System;
            public readonly bool Critical;
            public Entry(string id, string system, bool critical) { Id = id; System = system; Critical = critical; }
        }

        public static readonly IReadOnlyList<Entry> All = Build();

        private static List<Entry> Build()
        {
            var list = new List<Entry>();
            void Add(string id, string system, bool critical = false) => list.Add(new Entry(id, system, critical));
            // ALARMS-BEGIN
            Add("rx_temp_warn", "reactor"); Add("rx_temp_crit", "reactor", true); Add("rx_temp_low", "reactor"); Add("rx_valve_low", "reactor");
            Add("rx_flow_low", "reactor"); Add("rx_pump0_broken", "reactor"); Add("rx_pump1_broken", "reactor"); Add("rx_scram_auto", "reactor", true);
            Add("pw_v_low", "power"); Add("pw_v_dark", "power", true); Add("pw_emergency_bus", "power"); Add("pw_battery_low", "power");
            Add("pw_breaker_tripped", "power"); Add("pw_overload", "power"); Add("pw_essential_short", "power"); Add("pw_surplus_high", "power");
            for (int c = 1; c <= 6; c++) { Add("wt_leak_" + c, "water"); Add("wt_level_" + c, "water"); Add("wt_flood_" + c, "water", true); }
            Add("wt_trim_high", "water"); Add("wt_list_high", "water"); Add("wt_bilge0_broken", "water"); Add("wt_bilge1_broken", "water");
            // ALARMS-END
            return list;
        }

        /// <summary>Plate code of a lamp: the system letter and its number inside the system (R1..R8, E1..E8, W1..W26).</summary>
        public static string Plate(int index)
        {
            char letter = All[index].System == "reactor" ? 'R' : All[index].System == "power" ? 'E' : 'W';
            int n = 1;
            for (int i = 0; i < index; i++) if (All[i].System == All[index].System) n++;
            return letter.ToString() + n;
        }
    }
}
