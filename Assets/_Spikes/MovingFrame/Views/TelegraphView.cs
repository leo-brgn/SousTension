using UnityEngine;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// Passive, diegetic machine telegraph (E3-08) in the central post (compartment 2): a brass dial whose handle sits on one of 5 notches
    /// (arriere, stop, lent, demi, toute) and a speed needle. The two floor tiles that move the handle are at x = -0.5, z = 4.2 (up) and z = 3.6
    /// (down), mirroring CONTROLS in server/src/controls.js. Handle and needle come from the authoritative propulsion state. Built from
    /// primitives for the spike; the brass telegraph art is E11.
    /// </summary>
    public sealed class TelegraphView : MonoBehaviour
    {
        private const float DialMaxSpeed = 6f;            // m/s, full-scale of the speed needle (server PROP_VMAX)
        private static readonly Vector3 DialPos = new Vector3(-2.88f, 1.3f, 3.9f);   // on the left wall, between the two floor tiles

        private MovingFrameModel _model;
        private Transform _handle, _needle;
        private float _handleAngle;

        public void Bind(MovingFrameModel model, Transform boat)
        {
            _model = model;
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

            // the two floor tiles (up / down)
            InteractableTarget.Mark(Tile(boat, "TelegraphUp", new Vector3(-0.5f, 0.04f, 4.2f), new Color(0.3f, 0.65f, 0.3f)), "tele_up");
            InteractableTarget.Mark(Tile(boat, "TelegraphDown", new Vector3(-0.5f, 0.04f, 3.6f), new Color(0.65f, 0.5f, 0.2f)), "tele_down");
        }

        private void Update()
        {
            if (_model == null) return;
            var p = _model.Propulsion;
            if (!p.Valid) return;
            // handle: notch i at -80 + 40 i degrees; the dial faces +x so rotation is about the x axis (towards +z for "ahead")
            _handleAngle = Mathf.MoveTowards(_handleAngle, -(-80f + 40f * p.Telegraph), 240f * Time.deltaTime);
            _handle.localRotation = Quaternion.AngleAxis(_handleAngle, Vector3.right);
            float t = Mathf.Clamp(p.Speed / DialMaxSpeed, -0.5f, 1f);
            _needle.localRotation = Quaternion.AngleAxis(-Mathf.Lerp(-30f, 120f, (t + 0.5f) / 1.5f), Vector3.right);
        }

        private static GameObject Tile(Transform parent, string name, Vector3 pos, Color color) => Block(parent, name, new Vector3(0.4f, 0.08f, 0.4f), pos, color);

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
