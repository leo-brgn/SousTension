using SousTension.Sim;
using UnityEngine;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// Passive view of the machine telegraph (E3-08): the handle shows the order given (arriere, stop, lent, demi, toute) and a small dial shows the speed
    /// of the boat. The handle is moved with the two floor pedals (boat-local x = -2.3, z = 6.15 for one notch up and 5.35 for one notch down, in the
    /// central post; <see cref="ControlLayout"/>, mirrored from server/src/controls.js). With the 3D boat the telegraph is the art's
    /// (Models/Central/EngineTelegraphFivePosition.fbx) and its handle swings between the five marked positions; without it, primitives.
    /// </summary>
    public sealed class TelegraphView : MonoBehaviour
    {
        private const float DialMaxSpeed = 6f;            // m/s, full-scale of the speed needle (server PROP_VMAX)
        private static readonly Vector3 DialPos = new Vector3(-2.88f, 1.3f, 5.75f);   // on the left wall, between the two floor pedals

        private MovingFrameModel _model;
        private Transform _handle, _needle;               // primitive mode
        private PartRig _artHandle, _artNeedle;           // art mode
        private float _handleAngle;

        public void Bind(MovingFrameModel model, Transform boat)
        {
            _model = model;
            var env = BoatEnvironmentView.Current;
            var wrapper = env != null ? env.Nearest("EngineTelegraphFivePosition", ControlLayout.TeleX, 0.5f * (ControlLayout.TeleUpZ + ControlLayout.TeleDownZ)) : null;
            if (wrapper != null) BindArt(wrapper); else BindPrimitives(boat);
            // the two floor pedals (up / down)
            InteractableTarget.Mark(Tile(boat, "TelegraphUp", new Vector3(ControlLayout.TeleX + 0.6f, 0.04f, ControlLayout.TeleUpZ), new Color(0.3f, 0.65f, 0.3f)), "tele_up");
            InteractableTarget.Mark(Tile(boat, "TelegraphDown", new Vector3(ControlLayout.TeleX + 0.6f, 0.04f, ControlLayout.TeleDownZ), new Color(0.65f, 0.5f, 0.2f)), "tele_down");
        }

        private void BindArt(Transform wrapper)
        {
            _artHandle = new PartRig(SpikeArt.Find(wrapper, "CommandHandle"), SpikeArt.Find(wrapper, "Mount_HandlePivot") is Transform pivot ? pivot.localPosition : Vector3.zero);
            SpikeArt.MakeAimable(SpikeArt.Find(wrapper, "CommandHandle"), "tele_up", 0.06f);
            var dial = SpikeArt.Spawn("GaugeRound_S", wrapper);
            if (dial != null)
            {
                dial.transform.localPosition = new Vector3(0f, 0.45f, 0.23f);
                _artNeedle = new PartRig(SpikeArt.Find(dial.transform, "Needle"));
            }
        }

        private void BindPrimitives(Transform boat)
        {
            var root = new GameObject("TelegraphStation").transform;
            root.SetParent(boat, false);
            root.localPosition = DialPos;

            Block(root, "Plate", new Vector3(0.06f, 0.7f, 0.7f), Vector3.zero, new Color(0.5f, 0.4f, 0.15f));
            // 5 notch marks on a half circle
            for (int i = 0; i < 5; i++)
            {
                float a = (-80f + 40f * i) * Mathf.Deg2Rad;
                Block(root, "Notch" + i, new Vector3(0.03f, 0.05f, 0.05f), new Vector3(0.05f, 0.27f * Mathf.Cos(a) + 0.05f, 0.27f * Mathf.Sin(a)), Color.white);
            }
            _handle = new GameObject("HandlePivot").transform;
            _handle.SetParent(root, false);
            _handle.localPosition = new Vector3(0.08f, 0.05f, 0f);
            Block(_handle, "Handle", new Vector3(0.05f, 0.25f, 0.05f), new Vector3(0f, 0.125f, 0f), new Color(0.15f, 0.1f, 0.05f));
            Block(_handle, "Knob", new Vector3(0.08f, 0.08f, 0.08f), new Vector3(0f, 0.27f, 0f), new Color(0.15f, 0.1f, 0.05f));
            // speed needle on a small dial below
            Block(root, "SpeedDial", new Vector3(0.05f, 0.22f, 0.22f), new Vector3(0.03f, -0.22f, 0f), new Color(0.9f, 0.88f, 0.75f));
            _needle = new GameObject("SpeedNeedlePivot").transform;
            _needle.SetParent(root, false);
            _needle.localPosition = new Vector3(0.07f, -0.22f, 0f);
            Block(_needle, "Needle", new Vector3(0.02f, 0.09f, 0.015f), new Vector3(0f, 0.045f, 0f), Color.black);
        }

        private void Update()
        {
            if (_model == null) return;
            var p = _model.Propulsion;
            if (!p.Valid) return;
            // handle: notch i at -80 + 40 i degrees; the dial faces +x so rotation is about the x axis (towards +z for "ahead")
            _handleAngle = Mathf.MoveTowards(_handleAngle, -(-80f + 40f * p.Telegraph), 240f * Time.deltaTime);
            if (_handle != null) _handle.localRotation = Quaternion.AngleAxis(_handleAngle, Vector3.right);
            if (_artHandle != null && _artHandle.Valid) _artHandle.Set(Vector3.forward, -70f * (_handleAngle / 80f));        // the five marks of the art are 35 degrees apart, +-70 at the ends
            float t = Mathf.Clamp(p.Speed / DialMaxSpeed, -0.5f, 1f);
            if (_needle != null) _needle.localRotation = Quaternion.AngleAxis(-Mathf.Lerp(-30f, 120f, (t + 0.5f) / 1.5f), Vector3.right);
            if (_artNeedle != null && _artNeedle.Valid) _artNeedle.Set(Vector3.forward, -240f * Mathf.Clamp01((t + 0.5f) / 1.5f));
        }

        private static GameObject Tile(Transform parent, string name, Vector3 pos, Color color) => Block(parent, name, new Vector3(0.4f, 0.08f, 0.4f), pos, color);

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
