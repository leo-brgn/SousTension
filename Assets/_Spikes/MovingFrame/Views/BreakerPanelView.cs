using System.Collections.Generic;
using SousTension.Sim;
using UnityEngine;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// Passive view of the 20 breakers of the main electrical panel (E3-07), on the left wall of compartment 5 (machines): 5 columns x 4 rows
    /// (<see cref="ControlLayout"/>, mirrored from server/src/power.js). With the 3D boat they are the art's 20 MachineBreakers of the ElectricalPanel
    /// (Models/Machines): the lever is up when closed, down when open, and glows red when tripped; the two dials show the bus voltage and the battery.
    /// Without the art each breaker is a small block that is green (closed), dark (open) or red (tripped).
    /// </summary>
    public sealed class BreakerPanelView : MonoBehaviour
    {
        private const int Count = ControlLayout.BreakerCount;
        private MovingFrameModel _model;
        private readonly Renderer[] _tiles = new Renderer[Count];              // primitive mode
        private readonly PartRig[] _levers = new PartRig[Count];               // art mode
        private readonly Renderer[] _leverRenderers = new Renderer[Count];
        private PartRig _voltageNeedle, _batteryNeedle;
        private bool _art;

        public void Bind(MovingFrameModel model, Transform boat)
        {
            _model = model;
            var env = BoatEnvironmentView.Current;
            if (env != null && BindArt(env)) { _art = true; return; }
            BindPrimitives(boat);
        }

        private bool BindArt(BoatEnvironmentView env)
        {
            var panel = env.Nearest("ElectricalPanel", ControlLayout.BreakerX(0), ControlLayout.BreakerZ(2));
            if (panel == null) return false;
            var breakers = new List<Transform>();
            foreach (var b in env.All("MachineBreaker"))
            {
                var p = env.BoatLocal(b);
                if (Mathf.Abs(p.x - ControlLayout.BreakerX(0)) < 0.5f && Mathf.Abs(p.z - ControlLayout.BreakerZ(2)) < 0.6f && p.y > 0.4f) breakers.Add(b);
            }
            breakers.Sort((p, q) =>
            {
                var a = env.BoatLocal(p); var c = env.BoatLocal(q);
                int rowA = Mathf.RoundToInt(a.y * 100f / 23f), rowC = Mathf.RoundToInt(c.y * 100f / 23f);        // rows are 0.23 m apart
                return rowA != rowC ? rowA.CompareTo(rowC) : a.z.CompareTo(c.z);
            });
            if (breakers.Count < Count) return false;
            for (int i = 0; i < Count; i++)
            {
                var lever = SpikeArt.Find(breakers[i], "Lever");
                _levers[i] = new PartRig(lever);
                if (lever != null) _leverRenderers[i] = lever.GetComponent<Renderer>();
                SpikeArt.MakeAimable(lever, InteractableIds.Breakers[i], 0.06f);
                SpikeArt.MakeAimable(SpikeArt.Find(breakers[i], "Body"), InteractableIds.Breakers[i], 0.04f);
            }
            // the panel's two dials: bus voltage and emergency battery
            var gauges = new List<Transform>();
            foreach (var g in env.All("GaugeRound_M"))
            {
                var p = env.BoatLocal(g);
                if (Mathf.Abs(p.x - ControlLayout.BreakerX(0)) < 0.5f && Mathf.Abs(p.z - ControlLayout.BreakerZ(2)) < 0.8f && p.y > 1.3f) gauges.Add(g);
            }
            if (gauges.Count >= 2) { _voltageNeedle = new PartRig(SpikeArt.Find(gauges[0], "Needle")); _batteryNeedle = new PartRig(SpikeArt.Find(gauges[1], "Needle")); }
            return true;
        }

        private void BindPrimitives(Transform boat)
        {
            for (int i = 0; i < Count; i++)
            {
                var go = SpikePrimitives.Create(PrimitiveType.Cube);
                go.name = "Breaker" + i;
                InteractableTarget.Mark(go, InteractableIds.Breakers[i]);
                go.transform.SetParent(boat, false);
                go.transform.localPosition = new Vector3(ControlLayout.BreakerX(i) - 0.45f, ControlLayout.BreakerHeight[ControlLayout.BreakerRow(i)], ControlLayout.BreakerZ(i));
                go.transform.localScale = new Vector3(0.1f, 0.12f, 0.14f);
                _tiles[i] = go.GetComponent<Renderer>();
            }
        }

        private void Update()
        {
            if (_model == null) return;
            var grid = _model.Grid;
            for (int i = 0; i < Count; i++)
            {
                int s = grid.Valid && i < grid.Breakers.Length ? grid.Breakers[i] : 1;   // 0 open, 1 closed, 2 tripped
                if (_tiles[i] != null) _tiles[i].material.color = s == 2 ? new Color(0.9f, 0.15f, 0.12f) : s == 1 ? new Color(0.25f, 0.7f, 0.3f) : new Color(0.2f, 0.2f, 0.2f);
                if (_levers[i] != null && _levers[i].Valid)
                {
                    _levers[i].Set(Vector3.right, s == 1 ? 28f : s == 2 ? 0f : -28f);
                    SpikeArt.Glow(_leverRenderers[i], new Color(1f, 0.1f, 0.08f), s == 2);                  // red: a breaker that tripped is a real fault
                }
            }
            if (!_art || !grid.Valid) return;
            if (_voltageNeedle != null && _voltageNeedle.Valid) _voltageNeedle.Set(Vector3.forward, -240f * Mathf.Clamp01(grid.Voltage));
            if (_batteryNeedle != null && _batteryNeedle.Valid) _batteryNeedle.Set(Vector3.forward, -240f * Mathf.Clamp01(grid.Battery));
        }
    }
}
