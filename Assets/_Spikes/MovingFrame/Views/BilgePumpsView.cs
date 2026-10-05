using UnityEngine;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// Passive, diegetic view of the two bilge pumps (E6-03): a pump body with a switch lever and a lamp (positions mirror BILGE_PUMPS in
    /// server/src/bilge.js). Lever up = switched on; lamp: green = moving water, amber = on but idle (dry, or no electricity), dark = stopped,
    /// red = broken (a real emergency). The water level itself is drawn by WaterView. Built from primitives for the spike.
    /// </summary>
    public sealed class BilgePumpsView : MonoBehaviour
    {
        // boat-local wall position of each pump and the direction the switch faces (+1 = faces +x, -1 = faces -x)
        private static readonly (Vector3 pos, float face)[] Pumps =
        {
            (new Vector3(-2.88f, 0f, 4.5f), 1f),
            (new Vector3(2.88f, 0f, -5.0f), -1f),
        };

        private MovingFrameModel _model;
        private readonly Transform[] _levers = new Transform[2];
        private readonly Renderer[] _lamps = new Renderer[2];

        public void Bind(MovingFrameModel model, Transform boat)
        {
            _model = model;
            for (int i = 0; i < Pumps.Length; i++)
            {
                var root = new GameObject("BilgePump" + i).transform;
                root.SetParent(boat, false);
                root.localPosition = Pumps[i].pos;
                float f = Pumps[i].face;
                InteractableTarget.Mark(Block(root, "Body", new Vector3(0.3f, 0.6f, 0.6f), new Vector3(f * 0.15f, 0.35f, 0f), new Color(0.25f, 0.3f, 0.35f)), InteractableIds.Bilge(i));
                var sw = new GameObject("Switch").transform;
                sw.SetParent(root, false);
                sw.localPosition = new Vector3(f * 0.34f, 0.55f, 0f);
                InteractableTarget.Mark(Block(sw, "Lever", new Vector3(0.04f, 0.2f, 0.04f), new Vector3(0f, 0.1f, 0f), new Color(0.9f, 0.88f, 0.75f)), InteractableIds.Bilge(i));
                _levers[i] = sw;
                _lamps[i] = Block(root, "Lamp", new Vector3(0.04f, 0.1f, 0.1f), new Vector3(f * 0.34f, 0.8f, 0f), Color.gray).GetComponent<Renderer>();
            }
        }

        private void Update()
        {
            if (_model == null) return;
            var b = _model.Bilge;
            if (!b.Valid) return;
            for (int i = 0; i < Pumps.Length && i < b.State.Length; i++)
            {
                bool on = b.State[i] == 1, broken = b.State[i] == 2, running = i < b.Running.Length && b.Running[i];
                _levers[i].localRotation = Quaternion.AngleAxis(on ? 35f : -35f, Vector3.right);
                _lamps[i].material.color = broken ? new Color(1f, 0.1f, 0.1f)
                    : running ? new Color(0.3f, 0.85f, 0.35f)
                    : on ? new Color(0.95f, 0.7f, 0.1f)
                    : new Color(0.25f, 0.25f, 0.25f);
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
