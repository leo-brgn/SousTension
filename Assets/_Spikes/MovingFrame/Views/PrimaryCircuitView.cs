using SousTension.Sim;
using UnityEngine;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// Passive, diegetic primary-circuit station (no HUD): four valve handwheels and two pump switches in compartment 4 (reactor), on the right wall
    /// (boat-local x = +2.6 for the valves, +2.0 for the pumps; positions mirror CONTROLS in server/src/controls.js through <see cref="ControlLayout"/>).
    /// A wheel turns as the valve opens (three turns from closed to open) and its indicator slides; a pump shows its switch (up = running), a spinning rotor
    /// and a lamp: green running, dark stopped, red broken. Everything comes from the authoritative reactor state. With the 3D boat the valves and the
    /// pumps are the art's (Models/Primary), animated through their parts; without it they are primitives.
    /// </summary>
    public sealed class PrimaryCircuitView : MonoBehaviour
    {
        private const float WallX = 2.88f;
        private const float WheelTurns = 3f;
        private const float IndicatorTravel = 0.075f;      // m, half of the indicator slide (Models/Primary README)

        private MovingFrameModel _model;
        private readonly Transform[] _wheels = new Transform[4];                 // primitive mode
        private readonly Transform[] _switches = new Transform[2];
        private readonly PartRig[] _artWheels = new PartRig[4];                  // art mode
        private readonly PartRig[] _artIndicators = new PartRig[4];
        private readonly PartRig[] _artSwitches = new PartRig[2];
        private readonly PartRig[] _artRotors = new PartRig[2];
        private readonly Renderer[] _lamps = new Renderer[2];
        private readonly float[] _rotorAngle = new float[2];

        public void Bind(MovingFrameModel model, Transform boat)
        {
            _model = model;
            var env = BoatEnvironmentView.Current;
            if (env != null && BindArt(env)) return;
            BindPrimitives(boat);
        }

        private bool BindArt(BoatEnvironmentView env)
        {
            bool any = false;
            for (int i = 0; i < 4; i++)
            {
                var valve = env.Nearest("PrimaryValve", ControlLayout.ValveX, ControlLayout.ValveZ[i]);
                if (valve == null) continue;
                any = true;
                string id = InteractableIds.Valve(i);
                var wheel = SpikeArt.Find(valve, "Wheel");
                var indicator = SpikeArt.Find(valve, "Indicator");
                _artWheels[i] = new PartRig(wheel);
                _artIndicators[i] = new PartRig(indicator);
                SpikeArt.MakeAimable(wheel, id, 0.08f);
                SpikeArt.MakeAimable(SpikeArt.Find(valve, "Body"), id, 0.08f);
            }
            for (int p = 0; p < 2; p++)
            {
                var pump = env.Nearest("PrimaryPump", ControlLayout.PumpX, ControlLayout.PumpZ[p]);
                if (pump == null) continue;
                any = true;
                string id = InteractableIds.Pump(p);
                var sw = SpikeArt.Find(pump, "Switch");
                _artSwitches[p] = new PartRig(sw);
                _artRotors[p] = new PartRig(SpikeArt.Find(pump, "Rotor"));
                SpikeArt.MakeAimable(sw, id, 0.1f);
                SpikeArt.MakeAimable(SpikeArt.Find(pump, "Cover"), id, 0.08f);
                SpikeArt.MakeAimable(SpikeArt.Find(pump, "Body"), id, 0.04f);
                var control = SpikeArt.Find(pump, "Mount_Control");
                _lamps[p] = Block(control != null ? control : pump, "PumpLamp" + p, new Vector3(0.06f, 0.06f, 0.03f), new Vector3(0f, 0.12f, 0.05f), Color.gray).GetComponent<Renderer>();
            }
            return any;
        }

        private void BindPrimitives(Transform boat)
        {
            var root = new GameObject("PrimaryCircuit").transform;
            root.SetParent(boat, false);

            // main pipe running along the wall behind the wheels
            Block(root, "Pipe", new Vector3(0.18f, 0.18f, 4.2f), new Vector3(WallX - 0.1f, 1.0f, -2.1f), new Color(0.35f, 0.3f, 0.25f));
            for (int i = 0; i < 4; i++)
            {
                float z = ControlLayout.ValveZ[i];
                InteractableTarget.Mark(Block(root, "ValveBody" + i, new Vector3(0.22f, 0.3f, 0.3f), new Vector3(WallX - 0.12f, 1.0f, z), new Color(0.6f, 0.5f, 0.2f)), InteractableIds.Valve(i));
                var pivot = new GameObject("Wheel" + i).transform;
                pivot.SetParent(root, false);
                pivot.localPosition = new Vector3(WallX - 0.3f, 1.25f, z);
                // rim (4 bars forming a square ring) + hub, in the plane facing the room (rotates about the x axis)
                InteractableTarget.Mark(Block(pivot, "RimA", new Vector3(0.05f, 0.34f, 0.04f), new Vector3(0f, 0f, 0.15f), new Color(0.75f, 0.1f, 0.1f)), InteractableIds.Valve(i));
                InteractableTarget.Mark(Block(pivot, "RimB", new Vector3(0.05f, 0.34f, 0.04f), new Vector3(0f, 0f, -0.15f), new Color(0.75f, 0.1f, 0.1f)), InteractableIds.Valve(i));
                InteractableTarget.Mark(Block(pivot, "RimC", new Vector3(0.05f, 0.04f, 0.34f), new Vector3(0f, 0.15f, 0f), new Color(0.75f, 0.1f, 0.1f)), InteractableIds.Valve(i));
                InteractableTarget.Mark(Block(pivot, "RimD", new Vector3(0.05f, 0.04f, 0.34f), new Vector3(0f, -0.15f, 0f), new Color(0.75f, 0.1f, 0.1f)), InteractableIds.Valve(i));
                InteractableTarget.Mark(Block(pivot, "Hub", new Vector3(0.07f, 0.08f, 0.08f), Vector3.zero, new Color(0.15f, 0.15f, 0.15f)), InteractableIds.Valve(i));
                _wheels[i] = pivot;
            }
            for (int p = 0; p < 2; p++)
            {
                float z = ControlLayout.PumpZ[p];
                InteractableTarget.Mark(Block(root, "PumpBody" + p, new Vector3(0.5f, 0.7f, 0.7f), new Vector3(ControlLayout.PumpX + 0.3f, 0.55f, z), new Color(0.25f, 0.35f, 0.3f)), InteractableIds.Pump(p));
                var sw = new GameObject("PumpSwitch" + p).transform;
                sw.SetParent(root, false);
                sw.localPosition = new Vector3(ControlLayout.PumpX + 0.03f, 0.7f, z);
                InteractableTarget.Mark(Block(sw, "Lever", new Vector3(0.04f, 0.2f, 0.04f), new Vector3(0f, 0.1f, 0f), new Color(0.9f, 0.88f, 0.75f)), InteractableIds.Pump(p));
                _switches[p] = sw;
                _lamps[p] = Block(root, "PumpLamp" + p, new Vector3(0.04f, 0.1f, 0.1f), new Vector3(ControlLayout.PumpX + 0.03f, 0.95f, z), Color.gray).GetComponent<Renderer>();
            }
        }

        private void Update()
        {
            if (_model == null) return;
            var rx = _model.Reactor;
            if (!rx.Valid) return;
            for (int i = 0; i < 4; i++)
            {
                float turn = rx.Valves[i] * WheelTurns * 360f;
                if (_wheels[i] != null) _wheels[i].localRotation = Quaternion.AngleAxis(turn, Vector3.right);
                if (_artWheels[i] != null && _artWheels[i].Valid) _artWheels[i].Set(Vector3.forward, turn);
                if (_artIndicators[i] != null && _artIndicators[i].Valid)
                {
                    var rig = _artIndicators[i];
                    rig.Set(Vector3.forward, 0f);                                    // back to the rest pose, then slide along x
                    rig.Part.localPosition += new Vector3(Mathf.Lerp(-IndicatorTravel, IndicatorTravel, Mathf.Clamp01(rx.Valves[i])), 0f, 0f);
                }
            }
            for (int p = 0; p < 2; p++)
            {
                bool broken = rx.PumpBroken[p];
                bool on = rx.Pumps[p];
                // switch lever: up when running (tilted toward +z), down when stopped or broken
                if (_switches[p] != null) _switches[p].localRotation = Quaternion.AngleAxis(on ? 35f : -35f, Vector3.right);
                if (_artSwitches[p] != null && _artSwitches[p].Valid) _artSwitches[p].Set(Vector3.right, on ? 25f : -25f);
                if (_artRotors[p] != null && _artRotors[p].Valid)
                {
                    _rotorAngle[p] += (on && !broken ? 540f : 0f) * Time.deltaTime;
                    _artRotors[p].Set(Vector3.right, _rotorAngle[p]);
                }
                // red is reserved for real emergencies: a broken pump is one
                if (_lamps[p] != null) _lamps[p].material.color = broken ? new Color(1f, 0.1f, 0.1f) : on ? new Color(0.3f, 0.85f, 0.35f) : new Color(0.25f, 0.25f, 0.25f);
            }
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
