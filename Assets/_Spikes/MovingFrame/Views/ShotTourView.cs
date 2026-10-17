using System.Collections;
using System.IO;
using UnityEngine;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// Developer tool (client option <c>--tour=DIR</c>): visits a list of viewpoints of the boat one after the other (the server must run in debug mode: it
    /// uses the console command <c>tp</c>), saves a screenshot of each one in DIR and quits. It lets the 3D boat be checked without playing:
    /// <c>SousTension.exe --device=tour0000000 --tour=Builds\Shots</c>.
    /// </summary>
    public sealed class ShotTourView : MonoBehaviour
    {
        // name, x, z, yaw (degrees: 0 = towards the bow, 90 = right, -90 = left, 180 = towards the stern), pitch (degrees, down positive)
        private static readonly (string name, float x, float z, float yaw, float pitch)[] Views =
        {
            ("01_radio_spawn", 0f, 2f, 0f, 5f),
            ("02_central_telegraph", -1.0f, 5.75f, -90f, 5f),
            ("03_central_hatch_from_central", 1.45f, 6.2f, 180f, 8f),
            ("04_reactor_console", 0f, -2f, -90f, 5f),
            ("05_reactor_scram_lever", -1.2f, -0.8f, -90f, 5f),
            ("06_reactor_valves_and_pumps", -0.2f, -2.1f, 90f, 8f),
            ("07_reactor_bilge_pump", 0f, -2.2f, 180f, 25f),
            ("08_machines_breaker_panel", -1.2f, -5.6f, -90f, 8f),
            ("09_corridor_bow_to_stern", 0f, 13.2f, 180f, 2f),
            ("10_living_quarters", 0f, -10.5f, 180f, 5f),
            ("11_torpedo_room", 0f, 10.8f, 0f, 5f),
            ("12_reactor_hatch_from_radio", -1.45f, 2.2f, 180f, 8f),
        };

        private MovingFrameModel _model;
        private DebugConsoleModel _console;
        private KeyboardInputSource _look;
        private string _dir;

        public void Bind(MovingFrameModel model, DebugConsoleModel console, KeyboardInputSource look, string dir)
        {
            _model = model; _console = console; _look = look; _dir = dir;
            Directory.CreateDirectory(dir);
            StartCoroutine(Run());
        }

        private string LightingReport()
        {
            var sb = new System.Text.StringBuilder();
            var grid = _model.Grid;
            sb.Append("grid valid=" + grid.Valid + " V=" + grid.Voltage.ToString("F2") + " emergency=" + grid.Emergency + " bands=[" + (grid.LightBands != null ? string.Join(",", grid.LightBands) : "") + "]");
            sb.Append(" ambient=" + RenderSettings.ambientMode + " " + RenderSettings.ambientLight);
            int n = 0;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (n++ < 14) sb.Append(" | " + l.name + " " + l.type + " i=" + l.intensity.ToString("F2") + " c=" + l.color + " r=" + l.range + " on=" + l.enabled);
            return sb.ToString();
        }

        private IEnumerator Run()
        {
            float waited = 0f;
            while ((string.IsNullOrEmpty(_model.LocalId) || !_model.HasServerTime) && waited < 40f) { waited += Time.unscaledDeltaTime; yield return null; }
            if (string.IsNullOrEmpty(_model.LocalId) || !_model.HasServerTime) { File.WriteAllText(Path.Combine(_dir, "failed.txt"), "not connected to the server"); Application.Quit(); yield return new WaitForSecondsRealtime(3f); System.Diagnostics.Process.GetCurrentProcess().Kill(); yield break; }
            yield return new WaitForSecondsRealtime(3f);
            // the state at the start of a patrol: veille, the grid is dark and the red emergency lamps are on
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime) { _look.Look(0f, 0.1f); yield return null; }
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(_dir, "00_start_dark_emergency.png"));
            yield return new WaitForSecondsRealtime(0.4f);
            _console.Submit("regime croisiere");
            _console.Submit("ff 300");
            yield return new WaitForSecondsRealtime(6f);
            foreach (var v in Views)
            {
                _console.Submit("tp " + v.x.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + v.z.ToString(System.Globalization.CultureInfo.InvariantCulture));
                for (float t = 0f; t < 1.5f; t += Time.unscaledDeltaTime) { _look.Look(v.yaw * Mathf.Deg2Rad, v.pitch * Mathf.Deg2Rad); yield return null; }
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(_dir, v.name + ".png"));
                Debug.Log("[Tour] " + v.name + " " + LightingReport());
                yield return new WaitForSecondsRealtime(0.4f);
            }
            File.WriteAllText(Path.Combine(_dir, "done.txt"), "ok");
            Application.Quit();
            yield return new WaitForSecondsRealtime(3f);
            System.Diagnostics.Process.GetCurrentProcess().Kill();            // disposing the network connection can keep a normal quit waiting
        }
    }
}
