using System;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// The player's preferences (pause panel): look sensitivity, inverted Y, camera steadiness, field of view, vsync, language, help panel.
    /// Pure data with clamping; saving is the job of <c>SettingsStore</c> (View layer), applying is done by whoever listens to <see cref="Changed"/>.
    /// </summary>
    public sealed class GameSettings
    {
        public const float SensitivityMin = 0.001f, SensitivityMax = 0.006f, SensitivityStep = 0.0005f;
        public const float FovMin = 60f, FovMax = 100f, FovStep = 5f;
        public const float StabilizationStep = 0.1f;

        public event Action Changed;

        private float _sensitivity = 0.0025f, _stabilization = 0.7f, _fov = 70f;
        public float Sensitivity { get => _sensitivity; set { _sensitivity = Clamp(Round(value, SensitivityStep), SensitivityMin, SensitivityMax); Changed?.Invoke(); } }
        public float Stabilization { get => _stabilization; set { _stabilization = Clamp(Round(value, StabilizationStep), 0f, 1f); Changed?.Invoke(); } }
        public float Fov { get => _fov; set { _fov = Clamp(Round(value, FovStep), FovMin, FovMax); Changed?.Invoke(); } }

        private bool _invertY, _vsync = true, _help;
        public bool InvertY { get => _invertY; set { _invertY = value; Changed?.Invoke(); } }
        public bool Vsync { get => _vsync; set { _vsync = value; Changed?.Invoke(); } }
        /// <summary>The right-hand help panel (what the aimed object is and what the keys do).</summary>
        public bool HelpVisible { get => _help; set { _help = value; Changed?.Invoke(); } }

        private string _language = "";
        /// <summary>"fr", "en", or "" for the default (command line / system).</summary>
        public string Language { get => _language; set { _language = value ?? ""; Changed?.Invoke(); } }

        private static float Round(float v, float step) => (float)Math.Round(v / step) * step;
        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}
