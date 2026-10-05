using System.Collections.Generic;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// State of the developer console (E1-09): the command lines waiting to be sent, the history (up / down arrows) and the log of what the server
    /// answered. Plain C#, no UnityEngine: testable in EditMode. The console is a developer tool of the editor and development builds only (the
    /// view is compiled out of release builds); the server ignores every command unless it runs in debug mode (SOUSTENSION_DEBUG=1).
    /// </summary>
    public sealed class DebugConsoleModel
    {
        public const int MaxLog = 200;
        public const int MaxLineLength = 120;      // the server refuses longer lines

        private readonly Queue<string> _pending = new Queue<string>();
        private readonly List<string> _history = new List<string>();
        private readonly List<string> _log = new List<string>();
        private int _historyCursor;

        public IReadOnlyList<string> Log => _log;
        public IReadOnlyList<string> History => _history;

        /// <summary>The player validated a line: it is logged, remembered, and sent with the next input tick.</summary>
        public void Submit(string line)
        {
            line = (line ?? "").Trim();
            if (line.Length == 0) return;
            if (line.Length > MaxLineLength) { Append("> line too long (max " + MaxLineLength + " characters)"); return; }
            Append("> " + line);
            if (_history.Count == 0 || _history[_history.Count - 1] != line) _history.Add(line);
            _historyCursor = _history.Count;
            _pending.Enqueue(line);
        }

        /// <summary>The next command to send (one per input tick), or null when there is none.</summary>
        public string TakeCommand() => _pending.Count > 0 ? _pending.Dequeue() : null;

        /// <summary>Lines of the server's answer.</summary>
        public void AddReply(string[] lines)
        {
            if (lines == null) return;
            foreach (var l in lines) Append(l);
        }

        /// <summary>Walk back (-1) or forward (+1) in the history; returns the line to show ("" past the newest).</summary>
        public string Recall(int direction)
        {
            if (_history.Count == 0) return "";
            _historyCursor = _historyCursor + (direction < 0 ? -1 : 1);
            if (_historyCursor < 0) _historyCursor = 0;
            if (_historyCursor >= _history.Count) { _historyCursor = _history.Count; return ""; }
            return _history[_historyCursor];
        }

        private void Append(string line)
        {
            _log.Add(line);
            if (_log.Count > MaxLog) _log.RemoveAt(0);
        }
    }
}
