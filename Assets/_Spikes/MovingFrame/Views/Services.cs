using UnityEngine;
using UnityEngine.InputSystem;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>Monotonic clock backed by Unity's real time.</summary>
    public sealed class UnityClockService : IClockService
    {
        public double Now => Time.realtimeSinceStartupAsDouble;
    }

    /// <summary>
    /// WASD + mouse look. Produces movement in BOAT-LOCAL axes (the look yaw is applied here, so the
    /// controller and the server never need to know about the camera).
    /// </summary>
    public sealed class KeyboardInputSource : IInputSource
    {
        public float Yaw { get; private set; }   // radians, about the boat's up axis
        public float Pitch { get; private set; } // radians

        private const float Sensitivity = 0.0025f;

        public void Read(out float moveX, out float moveZ)
        {
            var kb = Keyboard.current;
            float f = 0f, r = 0f;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.zKey.isPressed) f += 1f;
                if (kb.sKey.isPressed) f -= 1f;
                if (kb.dKey.isPressed) r += 1f;
                if (kb.aKey.isPressed || kb.qKey.isPressed) r -= 1f;
            }
            float sin = Mathf.Sin(Yaw), cos = Mathf.Cos(Yaw);
            moveX = f * sin + r * cos;
            moveZ = f * cos - r * sin;
        }

        /// <summary>Called every frame by the view layer to update the look angles.</summary>
        public void UpdateLook()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            var d = mouse.delta.ReadValue();
            Yaw += d.x * Sensitivity;
            Pitch = Mathf.Clamp(Pitch - d.y * Sensitivity, -1.2f, 1.2f);
        }
    }
}
