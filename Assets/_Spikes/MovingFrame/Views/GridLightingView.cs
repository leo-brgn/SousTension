using System.Collections.Generic;
using UnityEngine;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// Passive view: the scene lighting follows the electrical network (E3-07), the signature of the game: white -> orange -> red -> black as the
    /// bus voltage falls, with a faint red emergency glow on the battery bus while the grid is dark. The spike has one global light, so the six
    /// compartments' light bands (server/src/power.js) are averaged. Intensity and colour ease towards the target; the original scene lighting
    /// is restored when the view is destroyed.
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

        public void Bind(MovingFrameModel model)
        {
            _model = model;
            _ambientBase = RenderSettings.ambientIntensity;
        }

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
            Apply();
        }

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
            for (int i = 0; i < _lights.Count; i++)
                if (_lights[i] != null) { _lights[i].intensity = _baseIntensity[i]; _lights[i].color = _baseColor[i]; }
            RenderSettings.ambientIntensity = _ambientBase;
        }
    }
}
