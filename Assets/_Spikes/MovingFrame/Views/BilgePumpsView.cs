using SousTension.Sim;
using UnityEngine;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// Passive view of the two bilge pumps (E6-03): a switch (up = on), a lamp (green running, amber on but idle / unpowered, red broken) and, with the
    /// 3D boat, the art's pump (Models/Machines/BilgePump.fbx) whose rotor spins while it moves water. The pumps stand on the floor in the middle of
    /// compartments 4 and 5 (boat-local (0, -3.45) and (0, -7.35), <see cref="ControlLayout"/>, mirrored from server/src/bilge.js). Without the art
    /// they are primitives against the wall. The switch is an art breaker mounted on the pump's front.
    /// </summary>
    public sealed class BilgePumpsView : MonoBehaviour
    {
        private const int Count = 2;

        private MovingFrameModel _model;
        private readonly Transform[] _levers = new Transform[Count];           // primitive mode
        private readonly PartRig[] _artLevers = new PartRig[Count];            // art mode
        private readonly PartRig[] _artRotors = new PartRig[Count];
        private readonly float[] _rotorAngle = new float[Count];
        private readonly Renderer[] _lamps = new Renderer[Count];
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
            bool any = false;
            for (int i = 0; i < Count; i++)
            {
                var pump = env.Nearest("BilgePump", ControlLayout.BilgeX[i], ControlLayout.BilgeZ[i]);
                if (pump == null) continue;
                any = true;
                string id = InteractableIds.Bilge(i);
                _artRotors[i] = new PartRig(SpikeArt.Find(pump, "Rotor"));
                SpikeArt.MakeAimable(SpikeArt.Find(pump, "Body"), id, 0.04f);
                SpikeArt.MakeAimable(SpikeArt.Find(pump, "Cap"), id, 0.04f);
                // the switch: an art breaker on the pump's front face (the pump is symmetrical: its front is +z of the boat), with its lamp
                var sw = SpikeArt.Spawn("MachineBreaker", pump);
                if (sw != null)
                {
                    sw.transform.localPosition = new Vector3(-0.1f, 0.38f, -0.245f);          // the art frame is turned 180 degrees: the bow side is local -z
                    sw.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    var lever = SpikeArt.Find(sw.transform, "Lever");
                    _artLevers[i] = new PartRig(lever);
                    SpikeArt.MakeAimable(lever, id, 0.05f);
                    SpikeArt.MakeAimable(SpikeArt.Find(sw.transform, "Body"), id, 0.05f);
                }
                var lamp = SpikeArt.Spawn("LampDome", pump);
                if (lamp != null)
                {
                    lamp.transform.localPosition = new Vector3(0.1f, 0.38f, -0.245f);
                    lamp.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    var dome = SpikeArt.Find(lamp.transform, "Dome");
                    if (dome != null) _lamps[i] = dome.GetComponent<Renderer>();
                }
            }
            return any;
        }

        private void BindPrimitives(Transform boat)
        {
            for (int i = 0; i < Count; i++)
            {
                var root = new GameObject("BilgePump" + i).transform;
                root.SetParent(boat, false);
                root.localPosition = new Vector3(ControlLayout.BilgeX[i], 0f, ControlLayout.BilgeZ[i]);
                InteractableTarget.Mark(Block(root, "Body", new Vector3(0.6f, 0.6f, 0.5f), new Vector3(0f, 0.3f, 0f), new Color(0.25f, 0.3f, 0.35f)), InteractableIds.Bilge(i));
                var sw = new GameObject("Switch").transform;
                sw.SetParent(root, false);
                sw.localPosition = new Vector3(0.1f, 0.5f, 0.27f);
                InteractableTarget.Mark(Block(sw, "Lever", new Vector3(0.04f, 0.2f, 0.04f), new Vector3(0f, 0.1f, 0f), new Color(0.9f, 0.88f, 0.75f)), InteractableIds.Bilge(i));
                _levers[i] = sw;
                _lamps[i] = Block(root, "Lamp", new Vector3(0.1f, 0.1f, 0.04f), new Vector3(-0.1f, 0.55f, 0.27f), Color.gray).GetComponent<Renderer>();
            }
        }

        private void Update()
        {
            if (_model == null) return;
            var b = _model.Bilge;
            if (!b.Valid) return;
            for (int i = 0; i < Count && i < b.State.Length; i++)
            {
                bool on = b.State[i] == 1, broken = b.State[i] == 2, running = i < b.Running.Length && b.Running[i];
                if (_levers[i] != null) _levers[i].localRotation = Quaternion.AngleAxis(on ? 35f : -35f, Vector3.right);
                if (_artLevers[i] != null && _artLevers[i].Valid) _artLevers[i].Set(Vector3.right, on ? 25f : -25f);
                if (_artRotors[i] != null && _artRotors[i].Valid)
                {
                    _rotorAngle[i] += (running ? 720f : 0f) * Time.deltaTime;
                    _artRotors[i].Set(Vector3.up, _rotorAngle[i]);
                }
                Color c = broken ? new Color(1f, 0.1f, 0.1f) : running ? new Color(0.3f, 0.85f, 0.35f) : new Color(0.95f, 0.7f, 0.1f);
                if (_lamps[i] == null) continue;
                if (_art) SpikeArt.Glow(_lamps[i], c, broken || on);
                else _lamps[i].material.color = broken || on ? c : new Color(0.25f, 0.25f, 0.25f);
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
