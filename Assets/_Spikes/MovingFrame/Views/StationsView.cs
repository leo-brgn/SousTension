using System.Collections.Generic;
using SousTension.Sim;
using UnityEngine;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// Passive view of the commands of every coupled action (Rule of Two Players, E4-02, E4-03): two keys per action. Diegetic feedback, no HUD:
    /// grey idle; amber when pressed, with a lamp column above the key that shrinks as the 3 s window runs out (it blinks in the last second);
    /// green flash on success, red flash on failure (rules: StationFeedback). Sounds wait for the audio decision (E12-01).
    /// Positions mirror COUPLED_ACTIONS in server/src/coupled.js (the server sends the state, not the layout).
    /// </summary>
    public sealed class StationsView : MonoBehaviour
    {
        private static readonly (string id, Vector3 a, Vector3 b)[] Actions = BuildActions();

        private static (string id, Vector3 a, Vector3 b)[] BuildActions()
        {
            var table = ControlLayout.Coupled;
            var actions = new (string id, Vector3 a, Vector3 b)[table.Length];
            for (int i = 0; i < table.Length; i++)
                actions[i] = (table[i].id, new Vector3(table[i].ax, 0f, table[i].az), new Vector3(table[i].bx, 0f, table[i].bz));
            return actions;
        }

        private sealed class Pair
        {
            public readonly Renderer[] Keys = new Renderer[2];
            public readonly Transform[] Timers = new Transform[2];       // lamp column above each key: its height is the time left
            public readonly Renderer[] TimerRenderers = new Renderer[2];
            public float ResultAt = -100f;                                // Time.time when the latest outcome was first seen
            public string LastResult = "none";
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
                    var go = SpikePrimitives.Create(PrimitiveType.Cylinder);
                    go.name = "Key_" + action.id + (i == 0 ? "A" : "B");
                    InteractableTarget.Mark(go, InteractableIds.Command(action.id, i));
                    go.transform.SetParent(boat, false);
                    go.transform.localPosition = (i == 0 ? action.a : action.b) + new Vector3(0f, 0.6f, 0f);
                    go.transform.localScale = new Vector3(0.5f, 0.6f, 0.5f);
                    pair.Keys[i] = go.GetComponent<Renderer>();
                    var timer = SpikePrimitives.Create(PrimitiveType.Cylinder);
                    timer.name = "Timer_" + action.id + (i == 0 ? "A" : "B");
                    Destroy(timer.GetComponent<Collider>());
                    timer.transform.SetParent(boat, false);
                    timer.transform.localPosition = (i == 0 ? action.a : action.b) + new Vector3(0f, 1.3f, 0f);
                    timer.transform.localScale = new Vector3(0.12f, 0.001f, 0.12f);
                    pair.Timers[i] = timer.transform; pair.TimerRenderers[i] = timer.GetComponent<Renderer>();
                    timer.SetActive(false);
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
                    pair.ResultAt = Time.time;
                }
                pair.LastResult = state.Result;
                for (int i = 0; i < 2; i++)
                {
                    var fb = StationFeedback.Compute(i == 0 ? state.RemainingA : state.RemainingB, i == 0 ? state.HolderA : state.HolderB,
                                                     pair.LastResult, Time.time - pair.ResultAt);
                    Color key;
                    switch (fb.Look)
                    {
                        case KeyLook.Success: key = new Color(0.2f, 0.9f, 0.3f); break;
                        case KeyLook.Failure: key = new Color(0.9f, 0.2f, 0.2f); break;
                        case KeyLook.Armed: key = new Color(0.95f, 0.7f, 0.1f); break;
                        default: key = new Color(0.5f, 0.5f, 0.5f); break;
                    }
                    pair.Keys[i].material.color = key;
                    bool timerOn = fb.Look == KeyLook.Armed || fb.Look == KeyLook.Success || fb.Look == KeyLook.Failure;
                    if (pair.Timers[i].gameObject.activeSelf != timerOn) pair.Timers[i].gameObject.SetActive(timerOn);
                    if (!timerOn) continue;
                    // the column is the time left; in the last second of an armed key it blinks
                    float height = Mathf.Max(0.001f, fb.Fraction * 0.6f);
                    pair.Timers[i].localScale = new Vector3(0.12f, height, 0.12f);
                    var pos = pair.Timers[i].localPosition; pos.y = 1.0f + height; pair.Timers[i].localPosition = pos;   // the cylinder is 2 high: its centre rises with it
                    bool blinkOff = fb.Look == KeyLook.Armed && fb.Fraction < 0.34f && Mathf.FloorToInt(Time.time * 6f) % 2 == 0;
                    pair.TimerRenderers[i].enabled = !blinkOff;
                    pair.TimerRenderers[i].material.color = key;
                }
            }
        }
    }
}
