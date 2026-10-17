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
        private GameSettings _settings;
        private Func<string> _aimedTarget;
        private VisualElement _help;
        private VisualElement _hud;
        private Label _hudPrompt, _hudPromptAct;
        private readonly Label[] _hudSlot = new Label[3];
        private readonly string[] _hudSlotShown = { null, null, null };
        private string _hudShownTarget = "?";
        private readonly List<Action> _settingRefresh = new List<Action>();
        private bool _rebuild;

        public void Bind(MovingFrameModel model, Func<string> connectionStatus, GameSettings settings = null, Func<string> aimedTarget = null)
        {
            Instance = this;
            _model = model; _connectionStatus = connectionStatus;
            _settings = settings ?? new GameSettings(); _aimedTarget = aimedTarget;
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            panelSettings.themeStyleSheet = ScriptableObject.CreateInstance<ThemeStyleSheet>();       // empty: every style is set here
            var doc = gameObject.AddComponent<UIDocument>();
            doc.panelSettings = panelSettings;
            _root = doc.rootVisualElement;
            _root.style.flexGrow = 1;
            _root.style.unityFontDefinition = FontDefinition.FromFont(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));      // the empty theme brings no font: the texts are inherited from here
            BuildAll();
            ShowOnly(_title);
        }

        private void BuildAll()
        {
            _settingRefresh.Clear();
            BuildTitle(); BuildLobby(); BuildBriefing(); BuildDebrief(); BuildPause(); BuildStopped(); BuildHelp(); BuildHud();
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
            var card = Card(_pause, 820);
            Text(card, UiText.Get("ui.pause.title"), 52, Cream, true, true);
            Text(card, UiText.Get("ui.pause.note"), 22, Brass, true);
            Text(card, UiText.Get("ui.settings.title"), 28, Brass, false, true).style.marginTop = 12;
            var g = _settings;
            StepRow(card, "ui.settings.sensitivity", () => (g.Sensitivity / 0.0025f * 100f).ToString("0") + " %", () => g.Sensitivity -= GameSettings.SensitivityStep, () => g.Sensitivity += GameSettings.SensitivityStep);
            ToggleRow(card, "ui.settings.invert", () => g.InvertY, () => g.InvertY = !g.InvertY);
            StepRow(card, "ui.settings.stab", () => Mathf.RoundToInt(g.Stabilization * 100f) + " %", () => g.Stabilization -= GameSettings.StabilizationStep, () => g.Stabilization += GameSettings.StabilizationStep);
            StepRow(card, "ui.settings.fov", () => g.Fov.ToString("0") + "°", () => g.Fov -= GameSettings.FovStep, () => g.Fov += GameSettings.FovStep);
            ToggleRow(card, "ui.settings.vsync", () => g.Vsync, () => g.Vsync = !g.Vsync);
            ToggleRow(card, "ui.settings.help", () => g.HelpVisible, () => g.HelpVisible = !g.HelpVisible);
            ChoiceRow(card, "ui.settings.language", () => UiText.Get("ui.settings.lang." + UiText.Language), () =>
            {
                g.Language = UiText.Language == "fr" ? "en" : "fr";
                UiText.Language = g.Language;
                _rebuild = true;                                       // every text is built once: build them again in the new language
            });
            MakeButton(card, UiText.Get("ui.pause.resume"), () => _pauseOpen = false);
            MakeButton(card, UiText.Get("ui.quit"), () => QuitRequested?.Invoke(), Rust);
        }

        private VisualElement SettingRow(VisualElement card, string labelKey)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row; row.style.alignItems = Align.Center; row.style.justifyContent = Justify.SpaceBetween; row.style.marginTop = 6;
            var label = Text(row, UiText.Get(labelKey), 24, Cream);
            label.style.flexGrow = 1; label.style.marginBottom = 0;
            card.Add(row);
            return row;
        }

        private static Button SmallButton(VisualElement parent, string text, Action onClick)
        {
            var b = MakeButton(parent, text, onClick);
            b.style.fontSize = 24; b.style.marginTop = 0; b.style.paddingTop = 4; b.style.paddingBottom = 4; b.style.minWidth = 52;
            return b;
        }

        private void Persist() { SettingsStore.Save(_settings); }

        // a value changed with - and + (the cursor is free in the pause: plain buttons need no slider theme)
        private void StepRow(VisualElement card, string labelKey, Func<string> value, Action dec, Action inc)
        {
            var row = SettingRow(card, labelKey);
            SmallButton(row, "-", () => { dec(); Persist(); });
            var v = Text(row, "", 24, Brass, true); v.style.minWidth = 110; v.style.marginBottom = 0;
            SmallButton(row, "+", () => { inc(); Persist(); });
            _settingRefresh.Add(() => v.text = value());
        }

        private void ToggleRow(VisualElement card, string labelKey, Func<bool> value, Action toggle)
        {
            var row = SettingRow(card, labelKey);
            var b = SmallButton(row, "", () => { toggle(); Persist(); });
            b.style.minWidth = 110;
            _settingRefresh.Add(() => b.text = UiText.Get(value() ? "ui.settings.on" : "ui.settings.off"));
        }

        private void ChoiceRow(VisualElement card, string labelKey, Func<string> value, Action next)
        {
            var row = SettingRow(card, labelKey);
            var b = SmallButton(row, "", () => { next(); Persist(); });
            b.style.minWidth = 160;
            _settingRefresh.Add(() => b.text = value());
        }

        // The help panel (right side, F1): what the aimed object is and what a click does, plus the keys. Off by default; the game itself has no HUD.
        private void BuildHelp()
        {
            _help = new VisualElement { name = "Help" };
            _help.style.position = Position.Absolute; _help.style.right = 24; _help.style.top = 120; _help.style.width = 440;
            _help.style.paddingLeft = 20; _help.style.paddingRight = 20; _help.style.paddingTop = 14; _help.style.paddingBottom = 14;
            _help.style.backgroundColor = Panel; _help.style.display = DisplayStyle.None;
            _help.style.borderLeftWidth = 4; _help.style.borderLeftColor = Brass;
            _help.pickingMode = PickingMode.Ignore;
            Text(_help, UiText.Get("ui.help.title"), 26, Brass, false, true);
            Text(_help, UiText.Get("ui.help.keys"), 20, Cream);
            _root.Add(_help);
        }

        // The player HUD (always on during the run): what the hands and the chest pocket hold, and what a click does on the object aimed at.
        private static readonly string[] SlotKeys = { "ui.hud.left", "ui.hud.right", "ui.hud.pocket" };

        private void BuildHud()
        {
            _hud = new VisualElement { name = "Hud" };
            _hud.style.position = Position.Absolute; _hud.style.left = 0; _hud.style.right = 0; _hud.style.bottom = 28;
            _hud.style.alignItems = Align.Center; _hud.style.display = DisplayStyle.None; _hud.pickingMode = PickingMode.Ignore;
            var prompt = new VisualElement();
            prompt.style.alignItems = Align.Center; prompt.style.marginBottom = 14; prompt.pickingMode = PickingMode.Ignore;
            _hudPrompt = Text(prompt, "", 30, Cream, true, true); _hudPrompt.style.marginBottom = 2;
            _hudPromptAct = Text(prompt, "", 22, Brass, true); _hudPromptAct.style.marginBottom = 0;
            _hud.Add(prompt);
            var bar = new VisualElement();
            bar.style.flexDirection = FlexDirection.Row; bar.pickingMode = PickingMode.Ignore;
            for (int i = 0; i < 3; i++)
            {
                var box = new VisualElement();
                box.style.width = 230; box.style.marginLeft = 8; box.style.marginRight = 8; box.style.paddingLeft = 12; box.style.paddingRight = 12; box.style.paddingTop = 6; box.style.paddingBottom = 6;
                box.style.backgroundColor = Panel; box.style.borderBottomWidth = 4; box.style.borderBottomColor = Brass; box.pickingMode = PickingMode.Ignore;
                Text(box, UiText.Get(SlotKeys[i]), 16, Brass).style.marginBottom = 0;
                _hudSlot[i] = Text(box, "", 24, Cream); _hudSlot[i].style.marginBottom = 0;
                bar.Add(box);
                _hudSlotShown[i] = null;
            }
            _hud.Add(bar);
            _root.Add(_hud);
            _hudShownTarget = "?";
        }

        private string ItemName(string cargoId)
        {
            if (string.IsNullOrEmpty(cargoId)) return UiText.Get("ui.hud.empty");
            foreach (var c in _model.Cargo) if (c.Id == cargoId) return UiText.Get("item." + c.Kind);
            return cargoId;
        }

        private void RefreshHud(bool visible)
        {
            _hud.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible) return;
            var hands = _model.HandsOf(_model.LocalId);
            for (int i = 0; i < 3; i++)
            {
                string name = ItemName(hands[i]);
                if (name == _hudSlotShown[i]) continue;
                _hudSlotShown[i] = name; _hudSlot[i].text = name;
                _hudSlot[i].style.color = string.IsNullOrEmpty(hands[i]) ? Brass : Cream;
            }
            string target = _aimedTarget != null ? _aimedTarget() : null;
            if (target == _hudShownTarget) return;
            _hudShownTarget = target;
            string key = UiText.HintKey(target);
            _hudPrompt.text = key != null ? UiText.Get(key + ".name") : "";
            _hudPromptAct.text = key != null ? UiText.Get(key + ".act") : "";
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
            if (_rebuild) { _rebuild = false; _root.Clear(); BuildAll(); _shownPhase = ""; }
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame) { _settings.HelpVisible = !_settings.HelpVisible; Persist(); }
            string phase = !mission.Valid ? "run" : mission.Phase;
            if (phase == "run" && keyboard != null && keyboard.escapeKey.wasPressedThisFrame) _pauseOpen = !_pauseOpen;
            if (phase != "run") _pauseOpen = false;
            string key = phase == "run" ? (mission.Valid && mission.Paused ? "stopped" : (_pauseOpen ? "pause" : "run")) : phase;
            if (key != _shownPhase)
            {
                _shownPhase = key;
                ShowOnly(key == "lobby" ? _lobby : key == "briefing" ? _briefing : key == "debrief" ? _debrief : key == "pause" ? _pause : key == "stopped" ? _stopped : null);
            }
            _help.style.display = _settings.HelpVisible && (key == "run" || key == "pause") ? DisplayStyle.Flex : DisplayStyle.None;
            RefreshHud(key == "run");
            if (key == "pause") foreach (var refresh in _settingRefresh) refresh();
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
