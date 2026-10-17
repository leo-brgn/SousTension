using System.Collections.Generic;
using SousTension.Sim;

namespace SousTension.Spikes.MovingFrame
{
    public enum CueKind { Hatch, Breaker, Pump, Telegraph, Footstep, Scram }

    /// <summary>One short sound to play now, with its loudness (already muffled by the bulkheads between the source and the listener).</summary>
    public readonly struct AudioCue
    {
        public readonly CueKind Kind;
        public readonly float Gain;
        public AudioCue(CueKind kind, float gain) { Kind = kind; Gain = gain; }
    }

    /// <summary>
    /// Finds the short sounds of the boat by comparing the state with the previous one: a hatch opens or closes, a breaker clicks, a pump starts or stops,
    /// the telegraph moves, the SCRAM falls, and the local player's footsteps. Pure: the first call only learns the state (nothing plays at the start).
    /// </summary>
    public sealed class AudioCueDetector
    {
        public const float StepLength = 0.75f;                 // m walked per footstep
        private const float ReactorZ = -2.0f, GridZ = -6.0f, TelegraphZ = 5.8f;

        private bool _known;
        private bool[] _doors = new bool[0];
        private int[] _breakers = new int[0];
        private bool[] _pumps = new bool[0];
        private int[] _bilge = new int[0];
        private int _telegraph;
        private bool _scram;
        private float _lastX, _lastZ, _walked;

        public void Detect(MovingFrameModel m, List<AudioCue> cues)
        {
            int here = BoatLayout.CompartmentAt(m.LocalZ);
            var water = m.Water;
            var doors = water.DoorOpen ?? new bool[0];
            var breakers = m.Grid.Valid ? m.Grid.Breakers : new int[0];
            var pumps = m.Reactor.Valid ? m.Reactor.Pumps : new bool[0];
            var bilge = m.Bilge.Valid ? m.Bilge.State : new int[0];
            int telegraph = m.Propulsion.Valid ? m.Propulsion.Telegraph : _telegraph;
            bool scram = m.Reactor.Valid && m.Reactor.Scram;

            if (_known)
            {
                for (int d = 0; d < doors.Length && d < _doors.Length; d++)
                    if (doors[d] != _doors[d]) cues.Add(new AudioCue(CueKind.Hatch, AudioMix.Reach(water, d, here)));
                float grid = AudioMix.Reach(water, BoatLayout.CompartmentAt(GridZ), here);
                for (int i = 0; i < breakers.Length && i < _breakers.Length; i++)
                    if (breakers[i] != _breakers[i]) cues.Add(new AudioCue(CueKind.Breaker, grid));
                float reactor = AudioMix.Reach(water, BoatLayout.CompartmentAt(ReactorZ), here);
                for (int i = 0; i < pumps.Length && i < _pumps.Length; i++)
                    if (pumps[i] != _pumps[i]) cues.Add(new AudioCue(CueKind.Pump, reactor));
                for (int i = 0; i < bilge.Length && i < _bilge.Length; i++)
                    if (bilge[i] != _bilge[i]) cues.Add(new AudioCue(CueKind.Pump, grid));
                if (telegraph != _telegraph) cues.Add(new AudioCue(CueKind.Telegraph, AudioMix.Reach(water, BoatLayout.CompartmentAt(TelegraphZ), here)));
                if (scram && !_scram) cues.Add(new AudioCue(CueKind.Scram, 1f));
                float dx = m.LocalX - _lastX, dz = m.LocalZ - _lastZ;
                float step = (float)System.Math.Sqrt(dx * dx + dz * dz);
                if (step < 1.5f) _walked += step; else _walked = 0f;                  // a teleport or a correction is not a walk
                while (_walked >= StepLength) { _walked -= StepLength; cues.Add(new AudioCue(CueKind.Footstep, 1f)); }
            }
            _known = true;
            _doors = (bool[])doors.Clone(); _breakers = (int[])breakers.Clone(); _pumps = (bool[])pumps.Clone(); _bilge = (int[])bilge.Clone();
            _telegraph = telegraph; _scram = scram; _lastX = m.LocalX; _lastZ = m.LocalZ;
        }
    }
}
