using System.Collections.Generic;
using SousTension.Sim;
using UnityEngine;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// Passive, diegetic RK-1 panel (no HUD): a three-position selector knob, four needle gauges (core temperature, steam,
    /// electricity, noise) and three lamps (alert, critical, SCRAM). Mounted on the left wall of compartment 4 (reactor), around boat-local
    /// x = -2.3, z = -2.0 (<see cref="ControlLayout"/>, mirrored from server/src/controls.js). Everything shown comes from the authoritative reactor
    /// gauges, so it lags like the real plant. With the 3D boat the console is the art's (Models/Reactor/RK1_Console.fbx with its instruments);
    /// without it the panel is built from primitives.
    /// </summary>
    public sealed class ReactorPanelView : MonoBehaviour
    {
        private const float WallX = -2.88f;       // panel plane (faces +x, toward the interior)
        private static readonly Vector3 Centre = new Vector3(WallX, 1.35f, ControlLayout.RegimeZ);

        private MovingFrameModel _model;
        private Transform _knobPivot;
        private Transform[] _needles = new Transform[4];
        private Renderer[] _lamps = new Renderer[3];
        private float _knobAngle;
        private PartRig _artKnob;
        private readonly PartRig[] _artNeedles = new PartRig[4];
        private bool _art;

        // dial ranges: temperature degC, steam bar, electricity MWe, noise 0..4
        private static readonly float[] DialMin = { 280f, 0f, 0f, 0f };
        private static readonly float[] DialMax = { 380f, 250f, 35f, 4f };

        public void Bind(MovingFrameModel model, Transform boat)
        {
            _model = model;
            var env = BoatEnvironmentView.Current;
            if (env != null && BindArt(env)) { _art = true; return; }
            BindPrimitives(boat);
        }

        private bool BindArt(BoatEnvironmentView env)
        {
            var console = env.Nearest("RK1_Console", ControlLayout.RegimeX, ControlLayout.RegimeZ);
            var selector = env.Nearest("SelectorRotary", ControlLayout.RegimeX, ControlLayout.RegimeZ);
            if (console == null || selector == null) return false;
            var knob = SpikeArt.Find(selector, "Knob");
            _artKnob = new PartRig(knob);
            SpikeArt.MakeAimable(knob, "regime", 0.08f);
            SpikeArt.MakeAimable(SpikeArt.Find(selector, "Body"), "regime", 0.08f);

            // the four gauges of the console, in order along the wall (temperature, steam, electricity, noise)
            var gauges = new List<Transform>();
            foreach (var g in env.All("GaugeRound_M"))
            {
                var p = env.BoatLocal(g);
                if (Mathf.Abs(p.x - ControlLayout.RegimeX) < 1.2f && Mathf.Abs(p.z - ControlLayout.RegimeZ) < 1.2f) gauges.Add(g);
            }
            for (int g = 0; g < 4 && g < gauges.Count; g++) _artNeedles[g] = new PartRig(SpikeArt.Find(gauges[g], "Needle"));

            // lamps: alert, critical, SCRAM, on the console's own mounts
            for (int l = 0; l < 3; l++)
            {
                var mount = SpikeArt.Find(console, "Mount_Lamp" + l);
                var lamp = mount != null ? SpikeArt.Spawn("LampDome", mount) : null;
                if (lamp != null) _lamps[l] = SpikeArt.Find(lamp.transform, "Dome").GetComponent<Renderer>();
            }
            return true;
        }

        private void BindPrimitives(Transform boat)
        {
            var root = new GameObject("RK1Panel").transform;
            root.SetParent(boat, false);
            root.localPosition = Centre;

            Block(root, "Plate", new Vector3(0.06f, 1.3f, 1.5f), Vector3.zero, new Color(0.18f, 0.28f, 0.2f));

            // Selector: dial plate + knob + pointer, rotating about the wall normal (x axis)
            InteractableTarget.Mark(Block(root, "SelectorBase", new Vector3(0.08f, 0.42f, 0.42f), new Vector3(0.05f, 0.42f, 0f), new Color(0.12f, 0.12f, 0.12f)), "regime");
            _knobPivot = new GameObject("KnobPivot").transform;
            _knobPivot.SetParent(root, false);
            _knobPivot.localPosition = new Vector3(0.1f, 0.42f, 0f);
            InteractableTarget.Mark(Block(_knobPivot, "Knob", new Vector3(0.08f, 0.28f, 0.28f), Vector3.zero, new Color(0.7f, 0.55f, 0.2f)), "regime");
            Block(_knobPivot, "Pointer", new Vector3(0.1f, 0.2f, 0.04f), new Vector3(0.02f, 0.12f, 0f), new Color(0.95f, 0.95f, 0.9f));
            // Position marks (Veille / Croisiere / Pleine)
            for (int i = 0; i < 3; i++)
            {
                float a = (-40f + 40f * i) * Mathf.Deg2Rad;
                Block(root, "Mark" + i, new Vector3(0.03f, 0.04f, 0.04f), new Vector3(0.1f, 0.42f + 0.27f * Mathf.Cos(a), -0.27f * Mathf.Sin(a)), Color.white);
            }

            // Four needle gauges in a row under the selector
            for (int g = 0; g < 4; g++)
            {
                float z = -0.55f + 0.37f * g;
                Block(root, "Dial" + g, new Vector3(0.05f, 0.3f, 0.3f), new Vector3(0.05f, -0.05f, z), new Color(0.9f, 0.88f, 0.75f));
                var pivot = new GameObject("NeedlePivot" + g).transform;
                pivot.SetParent(root, false);
                pivot.localPosition = new Vector3(0.09f, -0.05f, z);
                Block(pivot, "Needle", new Vector3(0.02f, 0.13f, 0.015f), new Vector3(0f, 0.065f, 0f), g == 0 ? new Color(0.8f, 0.1f, 0.1f) : Color.black);
                _needles[g] = pivot;
            }

            // Lamps: alert, critical, SCRAM
            for (int l = 0; l < 3; l++)
            {
                var lamp = Block(root, "Lamp" + l, new Vector3(0.05f, 0.1f, 0.1f), new Vector3(0.06f, -0.36f, -0.25f + 0.25f * l), Color.gray);
                _lamps[l] = lamp.GetComponent<Renderer>();
            }
        }

        private void Update()
        {
            if (_model == null) return;
            var rx = _model.Reactor;
            if (!rx.Valid) return;

            // Selector: smooth turn to the position of the regime
            int pos = rx.Regime == "pleine" ? 2 : rx.Regime == "croisiere" ? 1 : 0;
            _knobAngle = Mathf.MoveTowardsAngle(_knobAngle, -40f + 40f * pos, 360f * Time.deltaTime);
            if (_knobPivot != null) _knobPivot.localRotation = Quaternion.AngleAxis(_knobAngle, Vector3.right);
            if (_artKnob != null && _artKnob.Valid) _artKnob.Set(Vector3.forward, -_knobAngle);

            // Gauges: needle sweeps -120..+120 degrees over the dial range
            float[] values = { rx.Temp, rx.Steam, rx.Electricity, rx.Noise };
            for (int g = 0; g < 4; g++)
            {
                float t = Mathf.Clamp01((values[g] - DialMin[g]) / (DialMax[g] - DialMin[g]));
                if (_needles[g] != null) _needles[g].localRotation = Quaternion.AngleAxis(Mathf.Lerp(-120f, 120f, t), Vector3.right);
                if (_artNeedles[g] != null && _artNeedles[g].Valid) _artNeedles[g].Set(Vector3.forward, -240f * t);     // from the dial's zero (the needle's rest pose) clockwise
            }

            // Lamps (red is reserved for real emergencies: alert = amber, critical and SCRAM = red)
            Light(_lamps[0], rx.Warn, new Color(1f, 0.7f, 0.1f));
            Light(_lamps[1], rx.Crit, new Color(1f, 0.1f, 0.1f));
            Light(_lamps[2], rx.Scram, new Color(1f, 0.1f, 0.1f));
        }

        private void Light(Renderer r, bool on, Color color)
        {
            if (r == null) return;
            if (_art) SpikeArt.Glow(r, color, on); else r.material.color = on ? color : new Color(0.25f, 0.25f, 0.25f);
        }

        private static GameObject Block(Transform parent, string name, Vector3 scale, Vector3 localPos, Color color)
        {
            var go = SpikePrimitives.Create(PrimitiveType.Cube);
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
