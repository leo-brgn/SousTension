using System;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>The alarm sounds, by family and severity (docs/design/audio-direction.md section 3). One at a time: the most urgent un-acknowledged one.</summary>
    public enum AlarmTone { None, Water, Power, Reactor, DarkGrid, Flood, ReactorCritical }

    /// <summary>What the boat should sound like right now: levels and pitches of the machine loops and the alarm to ring. Pure data computed from the game state.</summary>
    public readonly struct AudioMix
    {
        public readonly float ReactorVolume, ReactorPitch;     // low hum: pitch follows the temperature
        public readonly float TurbineVolume, TurbinePitch;     // whistle: follows the steam pressure and the electricity made
        public readonly float GridVolume, GridPitch;           // 50 Hz hum of the machines: sags with the bus voltage, vanishes in a dark grid
        public readonly float WaterVolume;                     // trickle and slosh: rises with the worst compartment level and the leaks
        public readonly float HullVolume;                      // creaks: louder with depth
        public readonly AlarmTone Alarm;

        public AudioMix(float reactorVolume, float reactorPitch, float turbineVolume, float turbinePitch, float gridVolume, float gridPitch, float waterVolume, float hullVolume, AlarmTone alarm)
        {
            ReactorVolume = reactorVolume; ReactorPitch = reactorPitch; TurbineVolume = turbineVolume; TurbinePitch = turbinePitch;
            GridVolume = gridVolume; GridPitch = gridPitch; WaterVolume = waterVolume; HullVolume = hullVolume; Alarm = alarm;
        }

        public static readonly AudioMix Silent = new AudioMix(0, 1, 0, 1, 0, 1, 0, 0, AlarmTone.None);

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        /// <summary>The mix for the current state. During the lobby, briefing and debrief the boat is silent (the screens are in front).</summary>
        public static AudioMix Compute(MovingFrameModel m)
        {
            if (m.Mission.Valid && m.Mission.Phase != "run") return Silent;
            var r = m.Reactor;
            var g = m.Grid;
            bool dark = m.Alarms.ContainsKey("pw_v_dark") || (g.Valid && g.Voltage < 0.05f);
            float temp = r.Valid ? Clamp01((r.Temp - 250f) / 150f) : 0f;
            float reactorVolume = r.Valid && !r.Scram ? 0.25f + 0.35f * temp : 0f;
            float reactorPitch = 0.8f + 0.5f * temp;
            float turbineVolume = r.Valid && !r.Scram ? Clamp01(r.Electricity / 10f) * 0.4f : 0f;
            float turbinePitch = 0.8f + Clamp01(r.Steam / 60f) * 0.8f;
            float voltage = g.Valid ? Clamp01(g.Voltage) : 1f;
            float gridVolume = dark ? 0f : 0.2f * voltage;
            float gridPitch = 0.7f + 0.3f * voltage;
            float worst = 0f;
            var fill = m.Water.Fill;
            if (fill != null) foreach (var f in fill) if (f > worst) worst = f;
            float water = Clamp01(worst * 1.5f + (m.Leaks != null && m.Leaks.Length > 0 ? 0.25f : 0f));
            float hull = m.Boat.Valid ? Clamp01(m.Boat.Depth / 40f) * 0.5f : 0f;
            return new AudioMix(reactorVolume, reactorPitch, turbineVolume, turbinePitch, gridVolume, gridPitch, water, hull, PickAlarm(m, dark));
        }

        // Priority: critical reactor > flooding > dark grid > the other families; acknowledged alarms are silent.
        private static AlarmTone PickAlarm(MovingFrameModel m, bool dark)
        {
            var best = AlarmTone.None;
            foreach (var kv in m.Alarms)
            {
                if (kv.Value) continue;                                       // acknowledged: silent
                var tone = ToneOf(kv.Key);
                if (kv.Key == "pw_v_dark") tone = AlarmTone.DarkGrid;
                if (tone > best) best = tone;
            }
            return best;
        }

        private static AlarmTone ToneOf(string id)
        {
            if (id.StartsWith("wt_flood_")) return AlarmTone.Flood;
            if (id.StartsWith("wt_")) return AlarmTone.Water;
            if (id.StartsWith("pw_")) return AlarmTone.Power;
            if (id == "rx_temp_crit" || id == "rx_scram_auto") return AlarmTone.ReactorCritical;
            if (id.StartsWith("rx_")) return AlarmTone.Reactor;
            return AlarmTone.None;
        }
    }
}
