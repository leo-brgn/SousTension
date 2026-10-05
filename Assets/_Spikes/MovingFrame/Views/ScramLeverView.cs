using System.Collections.Generic;
using UnityEngine;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// Passive, diegetic SCRAM station (no HUD): a big red lever under a sealed cover, on the left wall of compartment 4, 1 m from the
    /// RK-1 selector (boat-local x = -2.9, z = -2.7; the interaction point is in server/src/controls.js), plus a depth needle gauge.
    /// Cover, lever and depth come from the authoritative state. After a SCRAM the lights go out (blackout) while the boat sinks.
    /// Built from primitives for the spike; the real lever art is E11-08 (Models/Reactor/ScramLever.fbx).
    /// </summary>
    public sealed class ScramLeverView : MonoBehaviour
    {
        private const float WallX = -2.88f;
        private static readonly Vector3 Centre = new Vector3(WallX, 1.2f, -2.7f);
        private const float DepthDialMax = 10f;        // m, full-scale of the depth needle
        private const float BlackoutLevel = 0.04f;     // fraction of the light left in the dark

        private MovingFrameModel _model;
        private Transform _cover, _lever, _depthNeedle;
        private readonly Renderer[] _stepLamps = new Renderer[3];
        private float _coverAngle, _leverAngle, _light = 1f;
        private readonly List<Light> _lights = new List<Light>();
        private readonly List<float> _lightBase = new List<float>();
        private float _ambientBase;

        public void Bind(MovingFrameModel model, Transform boat)
        {
            _model = model;
            var root = new GameObject("ScramStation").transform;
            root.SetParent(boat, false);
            root.localPosition = Centre;

            Block(root, "Plate", new Vector3(0.06f, 0.95f, 0.6f), Vector3.zero, new Color(0.18f, 0.28f, 0.2f));
            // lever: pivot at the plate centre, arm pointing up at rest, swings down along the wall when pulled
            _lever = new GameObject("LeverPivot").transform;
            _lever.SetParent(root, false);
            _lever.localPosition = new Vector3(0.08f, -0.1f, 0f);
            Block(_lever, "Arm", new Vector3(0.05f, 0.4f, 0.05f), new Vector3(0f, 0.2f, 0f), new Color(0.75f, 0.1f, 0.1f));
            Block(_lever, "Grip", new Vector3(0.09f, 0.09f, 0.09f), new Vector3(0f, 0.42f, 0f), new Color(0.75f, 0.1f, 0.1f));
            // sealed cover hinged at its top edge, swings up and out
            _cover = new GameObject("CoverPivot").transform;
            _cover.SetParent(root, false);
            _cover.localPosition = new Vector3(0.14f, 0.2f, 0f);
            Block(_cover, "Cover", new Vector3(0.05f, 0.62f, 0.34f), new Vector3(0f, -0.31f, 0f), new Color(0.55f, 0.58f, 0.55f));
            Block(_cover, "Seal", new Vector3(0.07f, 0.06f, 0.06f), new Vector3(0.02f, -0.6f, 0f), new Color(0.9f, 0.75f, 0.2f));
            // depth gauge above the lever
            Block(root, "DepthDial", new Vector3(0.05f, 0.24f, 0.24f), new Vector3(0.05f, 0.33f, 0f), new Color(0.9f, 0.88f, 0.75f));
            _depthNeedle = new GameObject("DepthNeedlePivot").transform;
            _depthNeedle.SetParent(root, false);
            _depthNeedle.localPosition = new Vector3(0.09f, 0.33f, 0f);
            Block(_depthNeedle, "Needle", new Vector3(0.02f, 0.1f, 0.015f), new Vector3(0f, 0.05f, 0f), Color.black);

            // restart procedure lamps (E3-05) under the lever: lever back, valves open, pumps running. They only light during a SCRAM.
            for (int i = 0; i < 3; i++)
                _stepLamps[i] = Block(root, "StepLamp" + i, new Vector3(0.04f, 0.07f, 0.07f), new Vector3(0.06f, -0.38f, -0.15f + 0.15f * i), Color.gray).GetComponent<Renderer>();

            _ambientBase = RenderSettings.ambientIntensity;
        }

        private void Update()
        {
            if (_model == null) return;
            var lv = _model.Lever;
            if (lv.Valid)
            {
                _coverAngle = Mathf.MoveTowards(_coverAngle, lv.CoverOpen ? 80f : 0f, 240f * Time.deltaTime);
                _cover.localRotation = Quaternion.AngleAxis(_coverAngle, Vector3.forward);
                // pulled down during a SCRAM, raised again once the restart procedure's first step (lever back) is done
                bool down = lv.Pulled && !_model.Restart.LeverBack;
                _leverAngle = Mathf.MoveTowards(_leverAngle, down ? -85f : 0f, 400f * Time.deltaTime);
                _lever.localRotation = Quaternion.AngleAxis(_leverAngle, Vector3.right);
            }
            var rs = _model.Restart;
            bool[] steps = { rs.LeverBack, rs.ValvesOpen, rs.PumpsRunning };
            for (int i = 0; i < 3; i++)
                _stepLamps[i].material.color = lv.Pulled && steps[i] ? new Color(0.3f, 0.85f, 0.35f) : new Color(0.25f, 0.25f, 0.25f);
            var boat = _model.Boat;
            if (boat.Valid)
                _depthNeedle.localRotation = Quaternion.AngleAxis(Mathf.Lerp(-120f, 120f, Mathf.Clamp01(boat.Depth / DepthDialMax)), Vector3.right);

            // blackout: every scene light (and the ambient) fades to almost nothing while the reactor is SCRAMmed
            bool dark = lv.Valid && lv.Pulled;
            _light = Mathf.MoveTowards(_light, dark ? BlackoutLevel : 1f, 4f * Time.deltaTime);
            ApplyLight();
        }

        private void ApplyLight()
        {
            if (_lights.Count == 0)
                foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) { _lights.Add(l); _lightBase.Add(l.intensity); }
            for (int i = 0; i < _lights.Count; i++) if (_lights[i] != null) _lights[i].intensity = _lightBase[i] * _light;
            RenderSettings.ambientIntensity = _ambientBase * _light;
        }

        private void OnDestroy()
        {
            // restore the scene lighting so leaving play mode never leaves the world dark
            for (int i = 0; i < _lights.Count; i++) if (_lights[i] != null) _lights[i].intensity = _lightBase[i];
            RenderSettings.ambientIntensity = _ambientBase;
        }

        private static GameObject Block(Transform parent, string name, Vector3 scale, Vector3 localPos, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().material.color = color;
            return go;
        }
    }
}
