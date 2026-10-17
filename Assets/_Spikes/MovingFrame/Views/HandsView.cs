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
        private readonly System.Collections.Generic.Dictionary<string, GameObject>[] _artHeld = { new System.Collections.Generic.Dictionary<string, GameObject>(), new System.Collections.Generic.Dictionary<string, GameObject>(), new System.Collections.Generic.Dictionary<string, GameObject>() };
        private static readonly Vector3[] Positions = { new Vector3(-0.28f, -0.24f, 0.5f), new Vector3(0.28f, -0.24f, 0.5f), new Vector3(0f, -0.34f, 0.42f) };
        private static readonly Vector3[] Scales = { new Vector3(0.2f, 0.2f, 0.2f), new Vector3(0.2f, 0.2f, 0.2f), new Vector3(0.1f, 0.1f, 0.1f) };

        // ---- the 3D arms (Models/Crew/FirstPersonArms.fbx) -------------------------------------------------------------------------------
        private const float ShoulderHeight = 1.07f;                  // the rig stands on the floor: its shoulders are 1.07 m up
        // The rig is a standing body: hands rest by the hips (out of view) and come up to 0.33 m in front of the shoulders when carrying or reaching.
        // The arms hang from a pivot at the shoulders, a little below and behind the eyes and pitched up 30 degrees, so that the raised hands show at the
        // bottom corners of the screen and the resting hands stay out of sight.
        private static readonly Vector3 ShoulderPivot = new Vector3(0f, -0.24f, -0.12f);
        private const float ArmsPitch = -30f;
        /// <summary>Developer tool: a pose forced on the arms (the screenshot tour shows the hands raised); null = follow the game.</summary>
        public static string ForcedPose;
        private Animator _arms;
        private IAimSource _aim;
        private IUseInput _use;
        private string _armState = "";
        private float _armStateUntil;
        private int _lastHeldCount;
        private bool _lastUse;

        private void BindArms(Transform camera)
        {
            var arms = SpikeArt.Spawn("FirstPersonArms", camera);
            if (arms == null) return;
            var pivot = new GameObject("ArmsPivot").transform;
            pivot.SetParent(camera, false);
            pivot.localPosition = ShoulderPivot;
            pivot.localRotation = Quaternion.Euler(ArmsPitch, 0f, 0f);
            arms.transform.SetParent(pivot, false);
            arms.transform.localPosition = new Vector3(0f, -ShoulderHeight, 0f);
            arms.transform.localRotation = Quaternion.identity;
            foreach (var r in arms.GetComponentsInChildren<Renderer>(true)) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; if (r is SkinnedMeshRenderer s) s.updateWhenOffscreen = true; }
            _arms = arms.GetComponentInChildren<Animator>(true);
        }

        private void PlayArms(string state, float holdSeconds = 0f)
        {
            if (_arms == null || state == _armState) return;
            _armState = state;
            _armStateUntil = holdSeconds > 0f ? Time.time + holdSeconds : 0f;
            _arms.CrossFadeInFixedTime("Base Layer." + state, 0.12f, 0, 0f);
        }

        /// <summary>Which pose the arms take: carrying, gripping (just took something), releasing (just dropped), turning a valve, reaching for what is aimed.</summary>
        private void UpdateArms(string[] held)
        {
            if (_arms == null) return;
            if (!string.IsNullOrEmpty(ForcedPose)) { PlayArms(ForcedPose); return; }
            int count = 0;
            foreach (var h in held) if (!string.IsNullOrEmpty(h)) count++;
            bool useHeld = _use != null && _use.UseHeld;
            string target = _aim != null ? _aim.TargetId : null;
            string rest = count > 0 ? "HandsCarry" : "HandsIdle";
            if (count > _lastHeldCount) PlayArms("HandsGrip", 0.5f);
            else if (count < _lastHeldCount) PlayArms("HandsRelease", 0.5f);
            else if (useHeld && target != null && target.StartsWith("valve")) PlayArms("HandsValve");
            else if (useHeld && !_lastUse && target != null) PlayArms("HandsReach", 0.6f);
            else if (_armStateUntil > 0f && Time.time < _armStateUntil) { }
            else if (_armState != rest && !(useHeld && target != null && target.StartsWith("valve"))) { _armStateUntil = 0f; _armState = ""; PlayArms(rest); }
            _lastHeldCount = count; _lastUse = useHeld;
        }

        public void Bind(MovingFrameModel model, Camera camera, IAimSource aim = null, IUseInput use = null)
        {
            _model = model; _aim = aim; _use = use;
            BindArms(camera.transform);
            for (int i = 0; i < 3; i++)
            {
                var go = SpikePrimitives.Create(PrimitiveType.Cube);
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
            UpdateArms(held);
            for (int i = 0; i < 3; i++)
            {
                bool any = held.Length > i && !string.IsNullOrEmpty(held[i]);
                if (_slots[i].gameObject.activeSelf != any) _slots[i].gameObject.SetActive(any);
                if (!any) continue;
                string kind = KindOf(held[i]);
                var art = ArtFor(i, kind);
                foreach (var kv in _artHeld[i]) kv.Value.SetActive(art != null && kv.Value == art);          // the held item is the 3D one when the art has it
                _slots[i].GetComponent<Renderer>().enabled = art == null;
                if (art == null) _slots[i].GetComponent<Renderer>().material.color = ColorOf(kind);
            }
        }

        private GameObject ArtFor(int slot, string kind)
        {
            if (_artHeld[slot].TryGetValue(kind, out var existing)) return existing;
            bool heavy = kind == "fuel";
            var go = SpikeArt.Spawn(SpikeArt.KeyForCargo(kind, heavy), _slots[slot]);
            if (go != null)
            {
                var inv = new Vector3(1f / Mathf.Max(_slots[slot].localScale.x, 1e-3f), 1f / Mathf.Max(_slots[slot].localScale.y, 1e-3f), 1f / Mathf.Max(_slots[slot].localScale.z, 1e-3f));
                go.transform.localScale = Vector3.Scale(inv, Vector3.one * (heavy ? 0.5f : 0.7f));      // the slot cube is scaled: undo it, then shrink the item to the hand
                go.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            }
            _artHeld[slot][kind] = go;                                                                    // null is remembered: no art for this kind
            return go;
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
