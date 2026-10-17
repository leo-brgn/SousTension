using System.Collections.Generic;
using SousTension.Sim;
using UnityEngine;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// Passive view: the scene lighting follows the electrical network (E3-07), the signature of the game: white -> orange -> red -> black as the
    /// bus voltage falls, with a faint red emergency glow on the battery bus while the grid is dark. With the 3D boat each compartment has its own lamp
    /// (E11-03) that follows the light band of ITS compartment (server/src/power.js) and the red emergency lamps come on only when the grid is dark; the
    /// scene's sun is turned down inside the hull. Without the art the lights of the scene are averaged. Intensity and colour ease towards the target;
    /// the original scene lighting is restored when the view is destroyed.
    /// </summary>
    public sealed class GridLightingView : MonoBehaviour
    {
        // band 3 white, 2 orange, 1 red, 0 dark
        private static readonly float[] BandIntensity = { 0.04f, 0.3f, 0.65f, 1f };
        private static readonly Color[] BandColor =
        {
            new Color(1f, 0.3f, 0.25f), new Color(1f, 0.45f, 0.3f), new Color(1f, 0.75f, 0.5f), Color.white
        };
        private static readonly Color EmergencyColor = new Color(1f, 0.2f, 0.15f);
        private const float EmergencyIntensity = 0.12f;
        private const float EaseSpeed = 3f;

        private MovingFrameModel _model;
        private readonly List<Light> _lights = new List<Light>();
        private readonly List<float> _baseIntensity = new List<float>();
        private readonly List<Color> _baseColor = new List<Color>();
        private float _ambientBase;
        private float _factor = 1f;
        private Color _tint = Color.white;

        private BoatEnvironmentView _env;
        private readonly float[] _compFactor = new float[6];
        private readonly Color[] _compTint = new Color[6];
        private List<Light> _envRoom, _envEmergency;
        private float[] _envRoomBase, _envEmergencyBase;
        private Color[] _envRoomColor;
        private int[] _envRoomComp, _envEmergencyComp;
        private UnityEngine.Rendering.AmbientMode _ambientModeBase;
        private Color _ambientColorBase;
        private const float InteriorSun = 0.12f;                   // the sun inside a hull: a trace, so the corners are not pitch black
        private static readonly Color InteriorAmbient = new Color(0.16f, 0.17f, 0.16f);

        public void Bind(MovingFrameModel model)
        {
            _model = model;
            _ambientBase = RenderSettings.ambientIntensity;
            _ambientModeBase = RenderSettings.ambientMode;
            _ambientColorBase = RenderSettings.ambientLight;
            for (int i = 0; i < _compFactor.Length; i++) { _compFactor[i] = 1f; _compTint[i] = Color.white; }
            _env = BoatEnvironmentView.Current;
            if (_env == null) return;
            _envRoom = new List<Light>(_env.RoomLights); _envEmergency = new List<Light>(_env.EmergencyLights);
            _envRoomBase = new float[_envRoom.Count]; _envRoomColor = new Color[_envRoom.Count]; _envRoomComp = new int[_envRoom.Count];
            _envEmergencyBase = new float[_envEmergency.Count]; _envEmergencyComp = new int[_envEmergency.Count];
            for (int i = 0; i < _envRoom.Count; i++) { _envRoomBase[i] = _envRoom[i].intensity; _envRoomColor[i] = _envRoom[i].color; _envRoomComp[i] = CompartmentOf(_envRoom[i]); }
            for (int i = 0; i < _envEmergency.Count; i++) { _envEmergencyBase[i] = _envEmergency[i].intensity; _envEmergencyComp[i] = CompartmentOf(_envEmergency[i]); }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        }

        private int CompartmentOf(Light l) => BoatLayout.CompartmentAt(_env.BoatLocal(l.transform).z);

        private void Update()
        {
            if (_model == null) return;
            float targetFactor = 1f;
            Color targetTint = Color.white;
            var grid = _model.Grid;
            if (grid.Valid && grid.LightBands.Length > 0)
            {
                float f = 0f, r = 0f, g = 0f, b = 0f;
                foreach (int band in grid.LightBands)
                {
                    int k = Mathf.Clamp(band, 0, 3);
                    f += BandIntensity[k]; r += BandColor[k].r; g += BandColor[k].g; b += BandColor[k].b;
                }
                int n = grid.LightBands.Length;
                targetFactor = f / n; targetTint = new Color(r / n, g / n, b / n);
                if (grid.Emergency) { targetFactor = Mathf.Max(targetFactor, EmergencyIntensity); targetTint = Color.Lerp(targetTint, EmergencyColor, 0.7f); }
            }
            float k2 = 1f - Mathf.Exp(-EaseSpeed * Time.deltaTime);
            _factor = Mathf.Lerp(_factor, targetFactor, k2);
            _tint = Color.Lerp(_tint, targetTint, k2);
            if (_env != null) { ApplyPerCompartment(grid, k2); return; }
            Apply();
        }

        // E11-03: every compartment's lamp follows the light band of its own compartment; the red lamps are the battery bus.
        private void ApplyPerCompartment(GridState grid, float k)
        {
            for (int c = 0; c < _compFactor.Length; c++)
            {
                int band = grid.Valid && c < grid.LightBands.Length ? Mathf.Clamp(grid.LightBands[c], 0, 3) : 3;
                _compFactor[c] = Mathf.Lerp(_compFactor[c], BandIntensity[band], k);
                _compTint[c] = Color.Lerp(_compTint[c], BandColor[band], k);
            }
            for (int i = 0; i < _envRoom.Count; i++)
            {
                if (_envRoom[i] == null) continue;
                int c = Mathf.Clamp(_envRoomComp[i], 0, _compFactor.Length - 1);
                _envRoom[i].enabled = true;
                _envRoom[i].intensity = _envRoomBase[i] * _compFactor[c];
                _envRoom[i].color = _envRoomColor[i] * _compTint[c];
            }
            float emergency = grid.Valid && grid.Emergency ? 1f : 0f;
            for (int i = 0; i < _envEmergency.Count; i++)
            {
                if (_envEmergency[i] == null) continue;
                _envEmergency[i].enabled = true;                                           // the art ships them switched off
                _envEmergency[i].intensity = Mathf.Lerp(_envEmergency[i].intensity < 0.01f ? 0f : _envEmergency[i].intensity, _envEmergencyBase[i] * emergency, k);
            }
            float avg = 0f;
            for (int c = 0; c < _compFactor.Length; c++) avg += _compFactor[c];
            avg /= _compFactor.Length;
            if (_sun.Count == 0)                                                      // the scene's suns, found once
                foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Directional) _sun[l] = l.intensity;
            foreach (var kv in _sun) if (kv.Key != null) kv.Key.intensity = kv.Value * InteriorSun * Mathf.Max(avg, 0.1f);
            RenderSettings.ambientLight = InteriorAmbient * Mathf.Max(avg, 0.15f);
        }

        private readonly Dictionary<Light, float> _sun = new Dictionary<Light, float>();

        private void Apply()
        {
            if (_lights.Count == 0)
                foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) { _lights.Add(l); _baseIntensity.Add(l.intensity); _baseColor.Add(l.color); }
            for (int i = 0; i < _lights.Count; i++)
            {
                if (_lights[i] == null) continue;
                _lights[i].intensity = _baseIntensity[i] * _factor;
                _lights[i].color = _baseColor[i] * _tint;
            }
            RenderSettings.ambientIntensity = _ambientBase * _factor;
        }

        private void OnDestroy()
        {
            RenderSettings.ambientMode = _ambientModeBase;
            RenderSettings.ambientLight = _ambientColorBase;
            foreach (var kv in _sun) if (kv.Key != null) kv.Key.intensity = kv.Value;
            for (int i = 0; i < _lights.Count; i++)
                if (_lights[i] != null) { _lights[i].intensity = _baseIntensity[i]; _lights[i].color = _baseColor[i]; }
            RenderSettings.ambientIntensity = _ambientBase;
        }
    }
}
