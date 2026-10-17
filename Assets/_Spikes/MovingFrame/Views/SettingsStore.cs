using UnityEngine;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>Saves and loads <see cref="GameSettings"/> in the player prefs (a local file of preferences, nothing about the game itself).</summary>
    public static class SettingsStore
    {
        private const string Prefix = "settings.";

        public static void Load(GameSettings s)
        {
            s.Sensitivity = PlayerPrefs.GetFloat(Prefix + "sens", s.Sensitivity);
            s.Stabilization = PlayerPrefs.GetFloat(Prefix + "stab", s.Stabilization);
            s.Fov = PlayerPrefs.GetFloat(Prefix + "fov", s.Fov);
            s.InvertY = PlayerPrefs.GetInt(Prefix + "inv", s.InvertY ? 1 : 0) != 0;
            s.Vsync = PlayerPrefs.GetInt(Prefix + "vsync", s.Vsync ? 1 : 0) != 0;
            s.HelpVisible = PlayerPrefs.GetInt(Prefix + "help", s.HelpVisible ? 1 : 0) != 0;
            s.Language = PlayerPrefs.GetString(Prefix + "lang", s.Language);
        }

        public static void Save(GameSettings s)
        {
            PlayerPrefs.SetFloat(Prefix + "sens", s.Sensitivity);
            PlayerPrefs.SetFloat(Prefix + "stab", s.Stabilization);
            PlayerPrefs.SetFloat(Prefix + "fov", s.Fov);
            PlayerPrefs.SetInt(Prefix + "inv", s.InvertY ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "vsync", s.Vsync ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "help", s.HelpVisible ? 1 : 0);
            PlayerPrefs.SetString(Prefix + "lang", s.Language);
            PlayerPrefs.Save();
        }
    }
}
