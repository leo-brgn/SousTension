using UnityEngine;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// First-person hands (E2-03), diegetic: what the local player holds is drawn in front of the camera, lower left (left hand), lower right
    /// (right hand) and bottom centre (chest pocket). A two-handed item shows in both hands. Colour = kind of item (patch, bucket, crate, torch...).
    /// Nothing is drawn for an empty hand. Other players' items are drawn by CargoView (they follow their carrier). Built from primitives for the
    /// spike; the real arms are E2-08.
    /// </summary>
    public sealed class HandsView : MonoBehaviour
    {
        private MovingFrameModel _model;
        private readonly Transform[] _slots = new Transform[3];
        private static readonly Vector3[] Positions = { new Vector3(-0.28f, -0.24f, 0.5f), new Vector3(0.28f, -0.24f, 0.5f), new Vector3(0f, -0.34f, 0.42f) };
        private static readonly Vector3[] Scales = { new Vector3(0.2f, 0.2f, 0.2f), new Vector3(0.2f, 0.2f, 0.2f), new Vector3(0.1f, 0.1f, 0.1f) };

        public void Bind(MovingFrameModel model, Camera camera)
        {
            _model = model;
            for (int i = 0; i < 3; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Hand" + (i == 0 ? "Left" : i == 1 ? "Right" : "Pocket");
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(camera.transform, false);
                go.transform.localPosition = Positions[i];
                go.transform.localScale = Scales[i];
                go.SetActive(false);
                _slots[i] = go.transform;
            }
        }

        private void Update()
        {
            if (_model == null || string.IsNullOrEmpty(_model.LocalId)) return;
            var held = _model.HandsOf(_model.LocalId);
            for (int i = 0; i < 3; i++)
            {
                bool any = held.Length > i && !string.IsNullOrEmpty(held[i]);
                if (_slots[i].gameObject.activeSelf != any) _slots[i].gameObject.SetActive(any);
                if (any) _slots[i].GetComponent<Renderer>().material.color = ColorOf(KindOf(held[i]));
            }
        }

        private string KindOf(string cargoId)
        {
            foreach (var c in _model.Cargo) if (c.Id == cargoId) return c.Kind;
            return "crate";
        }

        private static Color ColorOf(string kind)
        {
            switch (kind)
            {
                case "patch": return new Color(0.55f, 0.65f, 0.72f);
                case "bucket": return new Color(0.2f, 0.55f, 0.75f);
                case "flashlight": return new Color(0.95f, 0.9f, 0.5f);
                case "manual": return new Color(0.93f, 0.9f, 0.78f);
                case "fuel": return new Color(0.35f, 0.35f, 0.4f);
                default: return new Color(0.6f, 0.4f, 0.2f);
            }
        }
    }
}
