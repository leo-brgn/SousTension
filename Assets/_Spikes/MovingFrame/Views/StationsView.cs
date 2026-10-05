using System.Collections.Generic;
using UnityEngine;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// Passive view of the commands of every coupled action (Rule of Two Players, E4-02): two keys per action. Colour = authoritative
    /// state: grey idle, amber pressed (waiting for the other player), green flash on success, red flash on timeout.
    /// Positions mirror COUPLED_ACTIONS in server/src/coupled.js (the server sends the state, not the layout).
    /// </summary>
    public sealed class StationsView : MonoBehaviour
    {
        private static readonly (string id, Vector3 a, Vector3 b)[] Actions =
        {
            ("demo", new Vector3(0f, 0f, -9f), new Vector3(0f, 0f, 9f)),
            ("demo2", new Vector3(-2.5f, 0f, 6f), new Vector3(2.5f, 0f, -7f)),
            ("reactor_restart", new Vector3(-2.5f, 0f, -5f), new Vector3(2.5f, 0f, 8.5f)),
        };

        private sealed class Pair
        {
            public readonly Renderer[] Keys = new Renderer[2];
            public int LastResultTick = -1;
            public float FlashUntil;
            public Color FlashColor;
        }

        private MovingFrameModel _model;
        private readonly Dictionary<string, Pair> _pairs = new Dictionary<string, Pair>();

        public void Bind(MovingFrameModel model, Transform boat)
        {
            _model = model;
            foreach (var action in Actions)
            {
                var pair = new Pair();
                for (int i = 0; i < 2; i++)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    go.name = "Key_" + action.id + (i == 0 ? "A" : "B");
                    InteractableTarget.Mark(go, InteractableIds.Command(action.id, i));
                    go.transform.SetParent(boat, false);
                    go.transform.localPosition = (i == 0 ? action.a : action.b) + new Vector3(0f, 0.6f, 0f);
                    go.transform.localScale = new Vector3(0.5f, 0.6f, 0.5f);
                    pair.Keys[i] = go.GetComponent<Renderer>();
                }
                _pairs[action.id] = pair;
            }
        }

        private void Update()
        {
            if (_model == null) return;
            foreach (var kv in _pairs)
            {
                if (!_model.TryGetCoupled(kv.Key, out var state)) continue;
                var pair = kv.Value;
                if (state.ResultTick != pair.LastResultTick && state.Result != "none")
                {
                    pair.LastResultTick = state.ResultTick;
                    pair.FlashUntil = Time.time + 1.0f;
                    pair.FlashColor = state.Result == "success" ? new Color(0.2f, 0.9f, 0.3f) : new Color(0.9f, 0.2f, 0.2f);
                }
                for (int i = 0; i < 2; i++)
                {
                    bool pressed = (i == 0 ? state.RemainingA : state.RemainingB) > 0 || (i == 0 ? state.HolderA : state.HolderB) != "";
                    pair.Keys[i].material.color = Time.time < pair.FlashUntil ? pair.FlashColor
                        : pressed ? new Color(0.95f, 0.7f, 0.1f) : new Color(0.5f, 0.5f, 0.5f);
                }
            }
        }
    }
}
