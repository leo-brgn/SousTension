using SousTension.Sim;
using UnityEngine;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// The annunciator (E3-09c): the lamp panel of the alarms, on the right wall of the central post (boat-local x = +2.9, z = 6.6; the server checks the reach
    /// from <see cref="ControlLayout.AnnunciatorX"/>). One lamp per alarm of the catalogue (<see cref="AlarmCatalog"/>), a small plate with its code under
    /// it (system letter + number; what it means is in the Operating Manual). A ringing alarm lights its lamp and blinks until somebody presses the lamp
    /// (acknowledged: steady light while the cause lasts). Amber = warning; red is reserved for the real emergencies (critical alarms). Diegetic, no HUD.
    /// </summary>
    public sealed class AlarmPanelView : MonoBehaviour
    {
        private const int Columns = 8;
        private const float WallX = 2.93f;
        private const float PitchZ = 0.125f, PitchY = 0.16f;
        private static readonly Color Dark = new Color(0.24f, 0.2f, 0.16f);
        private static readonly Color Amber = new Color(1f, 0.62f, 0.1f);
        private static readonly Color Red = new Color(1f, 0.12f, 0.08f);

        private MovingFrameModel _model;
        private Renderer[] _lamps;
        private bool _art;

        public void Bind(MovingFrameModel model, Transform boat)
        {
            _model = model;
            int count = AlarmCatalog.All.Count;
            int rows = (count + Columns - 1) / Columns;
            _lamps = new Renderer[count];
            var root = new GameObject("Annunciator").transform;
            root.SetParent(boat, false);
            float width = Columns * PitchZ + 0.1f, height = rows * PitchY + 0.12f;
            float centreY = 1.7f;
            var plate = Block(root, "Plate", new Vector3(0.05f, height, width), new Vector3(WallX + 0.02f, centreY, ControlLayout.AnnunciatorZ), new Color(0.16f, 0.2f, 0.17f));
            _art = SpikeArt.Has("LampDome");
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            for (int i = 0; i < count; i++)
            {
                int row = i / Columns, col = i % Columns;
                float z = ControlLayout.AnnunciatorZ + (Columns - 1) * PitchZ / 2f - col * PitchZ;      // seen from the room (facing +x) the first column is on the left
                float y = centreY + (rows - 1) * PitchY / 2f - row * PitchY;
                string id = InteractableIds.Alarm(AlarmCatalog.All[i].Id);
                var lamp = Block(root, "Lamp_" + AlarmCatalog.All[i].Id, new Vector3(0.04f, 0.075f, 0.075f), new Vector3(WallX - 0.01f, y, z), Dark);
                InteractableTarget.Mark(lamp, id);
                _lamps[i] = lamp.GetComponent<Renderer>();
                var label = new GameObject("Plate_" + AlarmCatalog.Plate(i)).transform;                              // the code under the lamp, facing the room (-x)
                label.SetParent(root, false);
                label.localPosition = new Vector3(WallX - 0.03f, y - 0.058f, z);
                label.localRotation = Quaternion.Euler(0f, 90f, 0f);
                var text = label.gameObject.AddComponent<TextMesh>();
                text.font = font; label.GetComponent<MeshRenderer>().material = font.material;
                text.text = AlarmCatalog.Plate(i); text.fontSize = 24; text.characterSize = 0.012f; text.anchor = TextAnchor.MiddleCenter; text.color = new Color(0.85f, 0.82f, 0.7f);
            }
        }

        private void Update()
        {
            if (_model == null || _lamps == null) return;
            bool blink = Mathf.FloorToInt(Time.time * 3f) % 2 == 0;
            for (int i = 0; i < _lamps.Length; i++)
            {
                var entry = AlarmCatalog.All[i];
                Color colour = Dark;
                if (_model.Alarms.TryGetValue(entry.Id, out bool acknowledged) && (acknowledged || blink)) colour = entry.Critical ? Red : Amber;
                _lamps[i].material.color = colour;
                _lamps[i].material.SetColor("_EmissionColor", colour == Dark ? Color.black : colour * 1.2f);
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
