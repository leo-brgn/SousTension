using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// Aim and primary button of the first-person player (E2-02). Every frame it casts a ray from the camera; the first collider it hits decides:
    /// if it carries an <see cref="InteractableTarget"/> within arm's reach, that object is the aimed one. The aimed object lights up softly
    /// (emission, never a crosshair or any HUD: GDD rule). It implements <see cref="IAimSource"/> (the id sent to the server in <c>use</c>) and
    /// <see cref="IUseInput"/> (mouse left). The server re-checks the reach: the client range is only a convenience.
    /// </summary>
    public sealed class AimView : MonoBehaviour, IAimSource, IUseInput
    {
        public const float AimRange = 2.5f;                  // m; the server's arm's reach is 2.0 m (AIM_REACH)
        private static readonly Color Glow = new Color(0.35f, 0.3f, 0.12f);

        private Camera _camera;
        private string _target;
        private readonly List<Renderer> _lit = new List<Renderer>();

        public string TargetId => _target;
        public bool UseHeld => Mouse.current != null && Mouse.current.leftButton.isPressed;

        public void Bind(Camera camera) { _camera = camera; }

        private void Update()
        {
            string target = null;
            if (_camera != null)
            {
                var hits = Physics.RaycastAll(_camera.transform.position, _camera.transform.forward, AimRange, ~0, QueryTriggerInteraction.Ignore);
                float best = float.MaxValue;
                foreach (var hit in hits)
                {
                    if (hit.distance >= best) continue;       // the nearest solid object decides: a wall in front blocks the aim
                    best = hit.distance;
                    var marker = hit.collider.GetComponent<InteractableTarget>();
                    target = marker != null ? marker.Id : null;
                }
            }
            if (target == _target) return;
            Unlight();
            _target = target;
            if (_target != null) Light(_target);
        }

        private void Light(string id)
        {
            foreach (var t in InteractableTarget.All)
            {
                if (t == null || t.Id != id) continue;
                var r = t.GetComponent<Renderer>();
                if (r == null) continue;
                r.material.EnableKeyword("_EMISSION");
                r.material.SetColor("_EmissionColor", Glow);
                _lit.Add(r);
            }
        }

        private void Unlight()
        {
            foreach (var r in _lit) if (r != null) r.material.SetColor("_EmissionColor", Color.black);
            _lit.Clear();
        }

        private void OnDestroy() => Unlight();
    }
}
