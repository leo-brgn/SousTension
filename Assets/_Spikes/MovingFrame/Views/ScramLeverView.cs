using SousTension.Sim;
using UnityEngine;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// Passive, diegetic SCRAM station (no HUD): a big lever under a sealed cover, on the left wall of compartment 4 (reactor), 1 m from the RK-1 selector
    /// (boat-local x = -2.7, z = -0.8; the interaction point is in server/src/controls.js, mirrored by <see cref="ControlLayout"/>), plus a depth needle
    /// gauge and the restart procedure lamps. Cover, lever and depth come from the authoritative state. The blackout after a SCRAM is no longer faked here:
    /// the lights follow the electrical network's bus voltage (GridLightingView, E3-07), which the SCRAM drops to zero.
    /// With the 3D boat (BoatEnvironmentView) the lever and the cover are the art's (Models/Reactor/ScramLever.fbx), animated about their hinges;
    /// without it the station is built from primitives.
    /// </summary>
    public sealed class ScramLeverView : MonoBehaviour
    {
        private const float WallX = -2.88f;
        private static readonly Vector3 Centre = new Vector3(WallX, 1.2f, ControlLayout.ScramZ);
        private const float DepthDialMax = 10f;        // m, full-scale of the depth needle

        private MovingFrameModel _model;
        private Transform _cover, _lever, _depthNeedle;          // primitive mode
        private PartRig _artCover, _artLever, _artNeedle;        // art mode
        private readonly Renderer[] _stepLamps = new Renderer[3];
        private float _coverAngle, _leverAngle;

        public void Bind(MovingFrameModel model, Transform boat)
        {
            _model = model;
            var env = BoatEnvironmentView.Current;
            var wrapper = env != null ? env.Nearest("ScramLever", ControlLayout.ScramX, ControlLayout.ScramZ) : null;
            if (wrapper != null) BindArt(wrapper); else BindPrimitives(boat);
        }

        // ---- the 3D lever ------------------------------------------------------------------------------------------------------------------

        private void BindArt(Transform wrapper)
        {
            var cover = SpikeArt.Find(wrapper, "Cover");
            var lever = SpikeArt.Find(wrapper, "Lever");
            var seal = SpikeArt.Find(wrapper, "Seal");
            _artCover = new PartRig(cover, SpikeArt.BoundsPoint(cover, new Vector3(0f, 1f, -1f)));       // hinged at its top edge, on the wall side
            _artLever = new PartRig(lever, SpikeArt.BoundsPoint(lever, new Vector3(0f, 1f, 0f)));        // pivots at its top end
            foreach (var part in new[] { SpikeArt.Find(wrapper, "Base"), cover, lever, seal }) SpikeArt.MakeAimable(part, "scram");

            // the depth gauge above and the three restart lamps below (small instruments the art does not model on the lever itself)
            var dial = SpikeArt.Spawn("GaugeRound_S", wrapper);
            if (dial != null)
            {
                dial.transform.localPosition = new Vector3(0f, 0.95f, 0.06f);
                _artNeedle = new PartRig(SpikeArt.Find(dial.transform, "Needle"));
            }
            for (int i = 0; i < 3; i++)
            {
                var lamp = Block(wrapper, "StepLamp" + i, new Vector3(0.07f, 0.07f, 0.03f), new Vector3(-0.15f + 0.15f * i, -0.5f, 0.1f), Color.gray);
                _stepLamps[i] = lamp.GetComponent<Renderer>();
            }
        }

        // ---- primitives (no art) -----------------------------------------------------------------------------------------------------------

        private void BindPrimitives(Transform boat)
        {
            var root = new GameObject("ScramStation").transform;
            root.SetParent(boat, false);
            root.localPosition = Centre;

            InteractableTarget.Mark(Block(root, "Plate", new Vector3(0.06f, 0.95f, 0.6f), Vector3.zero, new Color(0.18f, 0.28f, 0.2f)), "scram");
            // lever: pivot at the plate centre, arm pointing up at rest, swings down along the wall when pulled
            _lever = new GameObject("LeverPivot").transform;
            _lever.SetParent(root, false);
            _lever.localPosition = new Vector3(0.08f, -0.1f, 0f);
            InteractableTarget.Mark(Block(_lever, "Arm", new Vector3(0.05f, 0.4f, 0.05f), new Vector3(0f, 0.2f, 0f), new Color(0.75f, 0.1f, 0.1f)), "scram");
            InteractableTarget.Mark(Block(_lever, "Grip", new Vector3(0.09f, 0.09f, 0.09f), new Vector3(0f, 0.42f, 0f), new Color(0.75f, 0.1f, 0.1f)), "scram");
            // sealed cover hinged at its top edge, swings up and out
            _cover = new GameObject("CoverPivot").transform;
            _cover.SetParent(root, false);
            _cover.localPosition = new Vector3(0.14f, 0.2f, 0f);
            InteractableTarget.Mark(Block(_cover, "Cover", new Vector3(0.05f, 0.62f, 0.34f), new Vector3(0f, -0.31f, 0f), new Color(0.55f, 0.58f, 0.55f)), "scram");
            Block(_cover, "Seal", new Vector3(0.07f, 0.06f, 0.06f), new Vector3(0.02f, -0.6f, 0f), new Color(0.9f, 0.75f, 0.2f));
            // depth gauge above the lever
            Block(root, "DepthDial", new Vector3(0.05f, 0.24f, 0.24f), new Vector3(0.05f, 0.33f, 0f), new Color(0.9f, 0.88f, 0.75f));
            _depthNeedle = new GameObject("DepthNeedlePivot").transform;
            _depthNeedle.SetParent(root, false);
            _depthNeedle.localPosition = new Vector3(0.09f, 0.33f, 0f);
            Block(_depthNeedle, "Needle", new Vector3(0.02f, 0.1f, 0.015f), new Vector3(0f, 0.05f, 0f), Color.black);

            // restart procedure lamps (E3-05) under the lever: lever back, valves open, pumps running. They only light during a SCRAM.
            for (int i = 0; i < 3; i++)
                _stepLamps[i] = Block(root, "StepLamp" + i, new Vector3(0.04f, 0.07f, 0.07f), new Vector3(0.06f, -0.38f, -0.15f + 0.15f * i), Color.gray).GetComponent<Renderer>();
        }

        private void Update()
        {
            if (_model == null) return;
            var lv = _model.Lever;
            if (lv.Valid)
            {
                _coverAngle = Mathf.MoveTowards(_coverAngle, lv.CoverOpen ? 80f : 0f, 240f * Time.deltaTime);
                // pulled down during a SCRAM, raised again once the restart procedure's first step (lever back) is done
                bool down = lv.Pulled && !_model.Restart.LeverBack;
                _leverAngle = Mathf.MoveTowards(_leverAngle, down ? -85f : 0f, 400f * Time.deltaTime);
                if (_cover != null) _cover.localRotation = Quaternion.AngleAxis(_coverAngle, Vector3.forward);
                if (_lever != null) _lever.localRotation = Quaternion.AngleAxis(_leverAngle, Vector3.right);
                if (_artCover != null && _artCover.Valid) _artCover.Set(Vector3.right, -_coverAngle);
                if (_artLever != null && _artLever.Valid) _artLever.Set(Vector3.right, _leverAngle);
            }
            var rs = _model.Restart;
            bool[] steps = { rs.LeverBack, rs.ValvesOpen, rs.PumpsRunning };
            for (int i = 0; i < 3; i++)
                if (_stepLamps[i] != null) _stepLamps[i].material.color = lv.Pulled && steps[i] ? new Color(0.3f, 0.85f, 0.35f) : new Color(0.25f, 0.25f, 0.25f);
            var boat = _model.Boat;
            if (!boat.Valid) return;
            float depthDeg = Mathf.Lerp(-120f, 120f, Mathf.Clamp01(boat.Depth / DepthDialMax));
            if (_depthNeedle != null) _depthNeedle.localRotation = Quaternion.AngleAxis(depthDeg, Vector3.right);
            if (_artNeedle != null && _artNeedle.Valid) _artNeedle.Set(Vector3.forward, -depthDeg);
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
