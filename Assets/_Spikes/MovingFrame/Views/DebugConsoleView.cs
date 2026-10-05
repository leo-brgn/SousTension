using UnityEngine;
using UnityEngine.InputSystem;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// The developer console (E1-09): open with the key left of 1 (` / ²), type a command (help lists them), Enter sends it, up / down recall the
    /// history. For developers and playtesters only: compiled out of release builds, and the server ignores every command unless it runs in debug
    /// mode (SOUSTENSION_DEBUG=1). It is not the game's HUD (the game has none).
    /// </summary>
    public sealed class DebugConsoleView : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>True while the console has the keyboard: the character does not move (see KeyboardInputSource).</summary>
        public static bool IsOpen { get; private set; }

        private DebugConsoleModel _model;
        private string _input = "";
        private Vector2 _scroll;
        private bool _focusInput;

        public void Bind(DebugConsoleModel model) { _model = model; }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.backquoteKey.wasPressedThisFrame) { IsOpen = !IsOpen; _focusInput = IsOpen; Cursor.lockState = IsOpen ? CursorLockMode.None : CursorLockMode.Locked; }
        }

        private void OnGUI()
        {
            if (!IsOpen || _model == null) return;
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Return) { _model.Submit(_input); _input = ""; _scroll.y = float.MaxValue; e.Use(); }
            else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.UpArrow) { _input = _model.Recall(-1); e.Use(); }
            else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.DownArrow) { _input = _model.Recall(1); e.Use(); }
            else if (e.type == EventType.KeyDown && e.character == '`') { e.Use(); }

            float h = Mathf.Min(Screen.height * 0.4f, 320f);
            GUI.Box(new Rect(0, 0, Screen.width, h), GUIContent.none);
            GUILayout.BeginArea(new Rect(6, 4, Screen.width - 12, h - 8));
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(h - 40));
            foreach (var line in _model.Log) GUILayout.Label(line);
            GUILayout.EndScrollView();
            GUI.SetNextControlName("dbginput");
            _input = GUILayout.TextField(_input, DebugConsoleModel.MaxLineLength);
            if (_focusInput) { GUI.FocusControl("dbginput"); _focusInput = false; }
            GUILayout.EndArea();
        }
#else
        public static bool IsOpen => false;
        public void Bind(DebugConsoleModel model) { }
#endif
    }
}
