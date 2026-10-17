using UnityEngine;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// The way the crew knows how far the mission has got, without a HUD: a log plate on the left wall of the central post (just aft of the telegraph) with the
    /// distance travelled in metres, the distance to go to the goal and a progress strip. Digits and a unit symbol only (no sentence to translate). Passive:
    /// it shows the authoritative mission (server/src/mission.js); without a mission (a bare match) it shows the boat\u0027s distance counter.
    /// </summary>
    public sealed class OdometerView : MonoBehaviour
    {
        private const float WallX = -2.93f;
        private const float CentreZ = 4.75f, CentreY = 1.6f;

        private MovingFrameModel _model;
        private TextMesh _distance, _togo;
        private Transform _strip;
        private int _shown = int.MinValue, _shownGoal = int.MinValue;

        public void Bind(MovingFrameModel model, Transform boat)
        {
            _model = model;
            var root = new GameObject("Odometer").transform;
            root.SetParent(boat, false);
            Block(root, "Plate", new Vector3(0.05f, 0.42f, 0.7f), new Vector3(WallX + 0.02f, CentreY, CentreZ), new Color(0.12f, 0.18f, 0.14f));
            Block(root, "StripBack", new Vector3(0.02f, 0.05f, 0.6f), new Vector3(WallX + 0.001f, CentreY - 0.15f, CentreZ), new Color(0.05f, 0.07f, 0.06f));
            var strip = Block(root, "Strip", new Vector3(0.02f, 0.05f, 0.6f), new Vector3(WallX - 0.001f, CentreY - 0.15f, CentreZ + 0.3f), new Color(0.85f, 0.65f, 0.2f));
            _strip = strip.transform;
            _distance = Digits(root, "Distance", new Vector3(WallX - 0.01f, CentreY + 0.05f, CentreZ), 0.02f, new Color(0.95f, 0.9f, 0.7f));
            _togo = Digits(root, "ToGo", new Vector3(WallX - 0.01f, CentreY - 0.075f, CentreZ), 0.011f, new Color(0.7f, 0.65f, 0.45f));
        }

        private TextMesh Digits(Transform parent, string name, Vector3 pos, float size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);              // readable from the room (the wall is at -x)
            var text = go.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.font = font; go.GetComponent<MeshRenderer>().material = font.material;
            text.fontSize = 48; text.characterSize = size; text.anchor = TextAnchor.MiddleCenter; text.color = color; text.text = "0 m";
            return text;
        }

        private void Update()
        {
            if (_model == null) return;
            var mission = _model.Mission;
            int distance = mission.Valid ? mission.DistanceMetres : Mathf.RoundToInt(_model.Propulsion.Valid ? _model.Propulsion.Distance : 0f);
            int goal = mission.Valid ? mission.GoalMetres : 0;
            if (distance == _shown && goal == _shownGoal) return;
            _shown = distance; _shownGoal = goal;
            _distance.text = distance + " m";
            _togo.text = goal > 0 ? Mathf.Max(0, goal - distance) + " m" : "";
            float fraction = goal > 0 ? Mathf.Clamp01(distance / (float)goal) : 0f;
            _strip.localScale = new Vector3(0.02f, 0.05f, Mathf.Max(0.001f, 0.6f * fraction));
            var p = _strip.localPosition; p.z = CentreZ + 0.3f - 0.3f * fraction; _strip.localPosition = p;                // grows from the left end seen from the room
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
