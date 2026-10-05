using UnityEngine;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// Passive, diegetic primary-circuit station (no HUD): four valve handwheels and two pump switches on the right wall of
    /// compartment 4 (boat-local x = +2.9; positions mirror CONTROLS in server/src/controls.js). A wheel turns as the valve opens
    /// (three turns from closed to open); a pump shows its switch (up = running) and a lamp: green running, dark stopped, red broken.
    /// Everything comes from the authoritative reactor state. Built from primitives for the spike; the real art is the pipe kit (E11).
    /// </summary>
    public sealed class PrimaryCircuitView : MonoBehaviour
    {
        private const float WallX = 2.88f;
        private static readonly float[] ValveZ = { -3.0f, -1.5f, 0.0f, 1.5f };
        private static readonly float[] PumpZ = { 3.5f, 5.0f };
        private const float WheelTurns = 3f;

        private MovingFrameModel _model;
        private readonly Transform[] _wheels = new Transform[4];
        private readonly Transform[] _switches = new Transform[2];
        private readonly Renderer[] _lamps = new Renderer[2];

        public void Bind(MovingFrameModel model, Transform boat)
        {
            _model = model;
            var root = new GameObject("PrimaryCircuit").transform;
            root.SetParent(boat, false);

            // main pipe running along the wall behind the wheels
            Block(root, "Pipe", new Vector3(0.18f, 0.18f, 10.5f), new Vector3(WallX - 0.1f, 1.0f, 0.9f), new Color(0.35f, 0.3f, 0.25f));
            for (int i = 0; i < 4; i++)
            {
                InteractableTarget.Mark(Block(root, "ValveBody" + i, new Vector3(0.22f, 0.3f, 0.3f), new Vector3(WallX - 0.12f, 1.0f, ValveZ[i]), new Color(0.6f, 0.5f, 0.2f)), InteractableIds.Valve(i));
                var pivot = new GameObject("Wheel" + i).transform;
                pivot.SetParent(root, false);
                pivot.localPosition = new Vector3(WallX - 0.3f, 1.25f, ValveZ[i]);
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
                InteractableTarget.Mark(Block(root, "PumpBody" + p, new Vector3(0.5f, 0.7f, 0.7f), new Vector3(WallX - 0.25f, 0.55f, PumpZ[p]), new Color(0.25f, 0.35f, 0.3f)), InteractableIds.Pump(p));
                var sw = new GameObject("PumpSwitch" + p).transform;
                sw.SetParent(root, false);
                sw.localPosition = new Vector3(WallX - 0.52f, 0.7f, PumpZ[p]);
                InteractableTarget.Mark(Block(sw, "Lever", new Vector3(0.04f, 0.2f, 0.04f), new Vector3(0f, 0.1f, 0f), new Color(0.9f, 0.88f, 0.75f)), InteractableIds.Pump(p));
                _switches[p] = sw;
                _lamps[p] = Block(root, "PumpLamp" + p, new Vector3(0.04f, 0.1f, 0.1f), new Vector3(WallX - 0.52f, 0.95f, PumpZ[p]), Color.gray).GetComponent<Renderer>();
            }
        }

        private void Update()
        {
            if (_model == null) return;
            var rx = _model.Reactor;
            if (!rx.Valid) return;
            for (int i = 0; i < 4; i++)
                _wheels[i].localRotation = Quaternion.AngleAxis(rx.Valves[i] * WheelTurns * 360f, Vector3.right);
            for (int p = 0; p < 2; p++)
            {
                bool broken = rx.PumpBroken[p];
                bool on = rx.Pumps[p];
                // switch lever: up when running (tilted toward +z), down when stopped or broken
                _switches[p].localRotation = Quaternion.AngleAxis(on ? 35f : -35f, Vector3.right);
                // red is reserved for real emergencies: a broken pump is one
                _lamps[p].material.color = broken ? new Color(1f, 0.1f, 0.1f) : on ? new Color(0.3f, 0.85f, 0.35f) : new Color(0.25f, 0.25f, 0.25f);
            }
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
