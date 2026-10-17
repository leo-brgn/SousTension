using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// The screens around the game (UI Toolkit): title, waiting room, mission order (briefing), debrief, pause and "submarine stopped". Passive: each screen shows
    /// what the model says (the mission flow of the server) and raises events for its buttons. NOT a HUD: during the run nothing is drawn (GDD zero HUD);
    /// the information stays on the instruments of the boat. All the texts come from <see cref="UiText"/>. While a screen is open the cursor is free and
    /// the camera does not turn (see <see cref="IsOpen"/>).
    /// </summary>
    public sealed class ScreensView : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }
        public static ScreensView Instance { get; private set; }

        /// <summary>Leave the title screen (the Play button; also used by the screenshot tour).</summary>
        public void StartGame() { _started = true; }

        public event Action<bool> ReadyChanged;
        public event Action QuitRequested;

        private static readonly Color Panel = new Color(0.07f, 0.1f, 0.09f, 0.92f);
        private static readonly Color Cream = new Color(0.93f, 0.9f, 0.78f);
        private static readonly Color Bottle = new Color(0.16f, 0.3f, 0.22f);
        private static readonly Color Brass = new Color(0.78f, 0.62f, 0.25f);
        private static readonly Color Rust = new Color(0.7f, 0.35f, 0.2f);

        private MovingFrameModel _model;
        private Func<string> _connectionStatus;
        private VisualElement _root, _title, _lobby, _briefing, _debrief, _pause, _stopped;
        private Label _titleStatus, _lobbyNeed, _lobbyList, _briefingObjective, _briefingCount, _debriefHead, _debriefWhy, _debriefStars, _debriefStats, _debriefWait;
        private Button _playButton, _readyButton, _againButton;
        private bool _started;                // the title screen has been left
        private bool _pauseOpen;
        private string _shownPhase = "";
        private bool _wantReady;

        public void Bind(MovingFrameModel model, Func<string> connectionStatus)
        {
            Instance = this;
            _model = model; _connectionStatus = connectionStatus;
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.themeStyleSheet = ScriptableObject.CreateInstance<ThemeStyleSheet>();       // empty: every style is set here
            var doc = gameObject.AddComponent<UIDocument>();
            doc.panelSettings = settings;
            _root = doc.rootVisualElement;
            _root.style.flexGrow = 1;
            _root.style.unityFontDefinition = FontDefinition.FromFont(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));      // the empty theme brings no font: the texts are inherited from here
            BuildTitle(); BuildLobby(); BuildBriefing(); BuildDebrief(); BuildPause(); BuildStopped();
            ShowOnly(_title);
        }

        // ---- building blocks --------------------------------------------------------------------------------------------------------------

        private VisualElement Screen(string name, bool dimmed = true)
        {
            var screen = new VisualElement { name = name };
            screen.style.position = Position.Absolute;
            screen.style.left = 0; screen.style.right = 0; screen.style.top = 0; screen.style.bottom = 0;
            screen.style.alignItems = Align.Center; screen.style.justifyContent = Justify.Center;
            screen.style.backgroundColor = dimmed ? new Color(0.02f, 0.03f, 0.03f, 0.72f) : new Color(0f, 0f, 0f, 0f);
            screen.style.display = DisplayStyle.None;
            _root.Add(screen);
            return screen;
        }

        private static VisualElement Card(VisualElement parent, float width)
        {
            var card = new VisualElement();
            card.style.width = width; card.style.paddingLeft = 48; card.style.paddingRight = 48; card.style.paddingTop = 36; card.style.paddingBottom = 36;
            card.style.backgroundColor = Panel;
            card.style.borderTopWidth = card.style.borderBottomWidth = card.style.borderLeftWidth = card.style.borderRightWidth = 3;
            card.style.borderTopColor = card.style.borderBottomColor = card.style.borderLeftColor = card.style.borderRightColor = Brass;
            card.style.alignItems = Align.Stretch;
            parent.Add(card);
            return card;
        }

        private static Label Text(VisualElement parent, string text, int size, Color color, bool center = false, bool bold = false)
        {
            var label = new Label(text);
            label.style.fontSize = size; label.style.color = color; label.style.whiteSpace = WhiteSpace.Normal;
            label.style.unityTextAlign = center ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
            if (bold) label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginBottom = 8;
            parent.Add(label);
            return label;
        }

        private static Button MakeButton(VisualElement parent, string text, Action onClick, Color? colour = null)
        {
            var b = new Button(onClick) { text = text };
            b.style.fontSize = 26; b.style.color = Cream; b.style.backgroundColor = colour ?? Bottle;
            b.style.paddingTop = 12; b.style.paddingBottom = 12; b.style.marginTop = 12;
            b.style.borderTopWidth = b.style.borderBottomWidth = b.style.borderLeftWidth = b.style.borderRightWidth = 2;
            b.style.borderTopColor = b.style.borderBottomColor = b.style.borderLeftColor = b.style.borderRightColor = Brass;
            parent.Add(b);
            return b;
        }

        // ---- the screens ------------------------------------------------------------------------------------------------------------------

        private void BuildTitle()
        {
            _title = Screen("Title");
            var card = Card(_title, 880);
            Text(card, UiText.Get("ui.title"), 84, Cream, true, true);
            Text(card, UiText.Get("ui.subtitle"), 22, Brass, true);
            _titleStatus = Text(card, "", 22, Cream, true);
            _playButton = MakeButton(card, UiText.Get("ui.play"), StartGame);
            MakeButton(card, UiText.Get("ui.quit"), () => QuitRequested?.Invoke(), Rust);
        }

        private void BuildLobby()
        {
            _lobby = Screen("Lobby");
            var card = Card(_lobby, 960);
            Text(card, UiText.Get("ui.lobby.title"), 52, Cream, false, true);
            Text(card, UiText.Get("ui.lobby.crew"), 26, Brass);
            _lobbyList = Text(card, "", 28, Cream);
            _lobbyNeed = Text(card, "", 24, Brass);
            _readyButton = MakeButton(card, UiText.Get("ui.ready"), ToggleReady);
            Text(card, UiText.Get("ui.controls.title"), 24, Brass).style.marginTop = 24;
            Text(card, UiText.Get("ui.controls.body"), 20, Cream);
        }

        private void BuildBriefing()
        {
            _briefing = Screen("Briefing");
            var card = Card(_briefing, 960);
            Text(card, UiText.Get("ui.briefing.title"), 52, Cream, false, true);
            _briefingObjective = Text(card, "", 34, Brass);
            Text(card, UiText.Get("ui.briefing.tip1"), 24, Cream);
            Text(card, UiText.Get("ui.briefing.tip2"), 24, Cream);
            Text(card, UiText.Get("ui.briefing.tip3"), 24, Cream);
            Text(card, UiText.Get("ui.briefing.tip4"), 24, Cream);
            _briefingCount = Text(card, "", 30, Brass, true, true);
        }

        private void BuildDebrief()
        {
            _debrief = Screen("Debrief");
            var card = Card(_debrief, 900);
            _debriefHead = Text(card, "", 58, Cream, true, true);
            _debriefWhy = Text(card, "", 26, Brass, true);
            _debriefStars = Text(card, "", 64, Brass, true);
            _debriefStats = Text(card, "", 26, Cream);
            _againButton = MakeButton(card, UiText.Get("ui.debrief.again"), ToggleReady);
            _debriefWait = Text(card, "", 22, Brass, true);
            MakeButton(card, UiText.Get("ui.quit"), () => QuitRequested?.Invoke(), Rust);
        }

        private void BuildPause()
        {
            _pause = Screen("Pause");
            var card = Card(_pause, 640);
            Text(card, UiText.Get("ui.pause.title"), 52, Cream, true, true);
            Text(card, UiText.Get("ui.pause.note"), 22, Brass, true);
            MakeButton(card, UiText.Get("ui.pause.resume"), () => _pauseOpen = false);
            MakeButton(card, UiText.Get("ui.quit"), () => QuitRequested?.Invoke(), Rust);
        }

        private void BuildStopped()
        {
            _stopped = Screen("Stopped");
            var card = Card(_stopped, 760);
            Text(card, UiText.Get("ui.paused.title"), 46, Cream, true, true);
            Text(card, UiText.Get("ui.paused.body"), 24, Brass, true);
        }

        private void ToggleReady()
        {
            _wantReady = !_model.LocalIsReady();
            ReadyChanged?.Invoke(_wantReady);
        }

        private void ShowOnly(VisualElement screen)
        {
            foreach (var s in new[] { _title, _lobby, _briefing, _debrief, _pause, _stopped })
                s.style.display = s == screen ? DisplayStyle.Flex : DisplayStyle.None;
            IsOpen = screen != null;
            UnityEngine.Cursor.lockState = IsOpen ? CursorLockMode.None : CursorLockMode.Locked;
            UnityEngine.Cursor.visible = IsOpen;
        }

        // ---- every frame ------------------------------------------------------------------------------------------------------------------

        private void Update()
        {
            if (_model == null) return;
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            var mission = _model.Mission;
            bool connected = !string.IsNullOrEmpty(_model.LocalId) && _model.HasServerTime;
            string status = _connectionStatus != null ? _connectionStatus() : "";

            if (!_started)
            {
                _titleStatus.text = connected ? UiText.Get("ui.connected") : (status.StartsWith("connection failed") ? UiText.Get("ui.connectionFailed") : UiText.Get("ui.connecting"));
                _playButton.SetEnabled(connected);
                if (_shownPhase != "title") { ShowOnly(_title); _shownPhase = "title"; }
                return;
            }

            // a server without a lobby (older, or a bare match) has no mission flow: the game is just running
            string phase = !mission.Valid ? "run" : mission.Phase;
            if (phase == "run" && keyboard != null && keyboard.escapeKey.wasPressedThisFrame) _pauseOpen = !_pauseOpen;
            if (phase != "run") _pauseOpen = false;
            string key = phase == "run" ? (mission.Valid && mission.Paused ? "stopped" : (_pauseOpen ? "pause" : "run")) : phase;
            if (key != _shownPhase)
            {
                _shownPhase = key;
                ShowOnly(key == "lobby" ? _lobby : key == "briefing" ? _briefing : key == "debrief" ? _debrief : key == "pause" ? _pause : key == "stopped" ? _stopped : null);
            }
            if (key == "lobby") RefreshLobby(mission);
            else if (key == "briefing") RefreshBriefing(mission);
            else if (key == "debrief") RefreshDebrief(mission);
        }

        private string ShortId(string id) => id.Length > 6 ? id.Substring(0, 6) : id;

        private void RefreshLobby(MissionState m)
        {
            var lines = new List<string>();
            var ids = new List<string>(_model.AllPlayerIds());
            var sorted = new List<string>(ids);
            sorted.Sort(StringComparer.Ordinal);                         // the same numbering on every screen: a sailor is "Sailor n" by the order of the sorted ids
            foreach (var id in ids)
            {
                bool ready = Array.IndexOf(m.ReadyIds, id) >= 0;
                string who = id == "ghost" ? UiText.Get("ui.lobby.ghost") : UiText.Format("ui.lobby.sailor", sorted.IndexOf(id) + 1);
                lines.Add((ready ? "■ " : "□ ") + who + (id == _model.LocalId ? " (" + UiText.Get("ui.lobby.you") + ")" : "") + " — " + UiText.Get(ready ? "ui.lobby.ready" : "ui.lobby.notReady"));
            }
            _lobbyList.text = string.Join("\n", lines);
            _lobbyNeed.text = m.Players < m.MinPlayers ? UiText.Format("ui.lobby.needMore", m.MinPlayers) : UiText.Get("ui.lobby.waiting");
            _readyButton.text = UiText.Get(_model.LocalIsReady() ? "ui.unready" : "ui.ready");
        }

        private void RefreshBriefing(MissionState m)
        {
            _briefingObjective.text = UiText.Format("ui.briefing.objective", m.GoalMetres);
            _briefingCount.text = UiText.Format("ui.briefing.countdown", m.BriefingSeconds);
        }

        private void RefreshDebrief(MissionState m)
        {
            bool success = m.Result == "success";
            _debriefHead.text = UiText.Get(success ? "ui.debrief.success" : "ui.debrief.failure");
            _debriefHead.style.color = success ? Cream : Rust;
            _debriefWhy.text = UiText.Get("ui.debrief.why." + m.Why);
            _debriefStars.text = new string('★', m.Stars) + new string('☆', 3 - m.Stars);
            string unit = UiText.Get("ui.unit.s"), metre = UiText.Get("ui.unit.m");
            _debriefStats.text = UiText.Get("ui.debrief.note") + "\n"
                + UiText.Get("ui.debrief.stat.time") + " : " + (m.StatSeconds / 60) + " min " + (m.StatSeconds % 60) + " " + unit + "\n"
                + UiText.Get("ui.debrief.stat.distance") + " : " + m.StatMetres + " " + metre + "\n"
                + UiText.Get("ui.debrief.stat.faults") + " : " + m.StatFaults + "\n"
                + UiText.Get("ui.debrief.stat.alarms") + " : " + m.StatAlarms + "\n"
                + UiText.Get("ui.debrief.stat.fill") + " : " + Mathf.RoundToInt(m.StatMaxFill * 100f) + " %\n"
                + UiText.Get("ui.debrief.stat.scrams") + " : " + m.StatScrams;
            _againButton.text = UiText.Get(_model.LocalIsReady() ? "ui.unready" : "ui.debrief.again");
            _debriefWait.text = UiText.Format("ui.debrief.waiting", m.ReadyIds.Length, m.Players);
        }

        private void OnDestroy() { IsOpen = false; }
    }
}
