using System;
using System.Collections.Generic;
using Adrenak.UniVoice;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// UniVoice's network side over the Nakama match (E8-07c). There is no voice server of its own: the authoritative match already relays the packets
    /// (server/src/match.js, OP_VOICE) and the model already knows who is in the boat, so peers appear and disappear with the players. UniVoice identifies peers by an
    /// int: the local sailor is 0, the others get 1, 2, ... in the order they are first seen. Mute and deafen come from <see cref="YourVoiceSettings"/> and apply here, on the
    /// receiving side.
    /// </summary>
    public sealed class MatchVoiceClient : IAudioClient<int>
    {
        private readonly IVoiceTransport _transport;
        private readonly Func<string> _localId;
        private readonly Func<IEnumerable<string>> _players;
        private readonly Dictionary<string, int> _peerOf = new Dictionary<string, int>();
        private readonly List<string> _gone = new List<string>();
        private readonly HashSet<string> _present = new HashSet<string>();
        private int _next = 1;
        private bool _joined;

        public int ID => 0;
        public List<int> PeerIDs { get; } = new List<int>();
        public VoiceSettings YourVoiceSettings { get; } = new VoiceSettings();

        public event Action<int, List<int>> OnJoined;
        public event Action OnLeft;
        public event Action<int> OnPeerJoined;
        public event Action<int> OnPeerLeft;
        public event Action<int, AudioFrame> OnReceivedPeerAudioFrame;

        public MatchVoiceClient(IVoiceTransport transport, Func<string> localId, Func<IEnumerable<string>> players)
        {
            _transport = transport; _localId = localId; _players = players;
            _transport.VoiceReceived += OnPacket;
        }

        /// <summary>Called every frame: follows the players of the model (joined, left).</summary>
        public void Tick()
        {
            string me = _localId();
            if (string.IsNullOrEmpty(me)) return;
            if (!_joined)
            {
                _joined = true;
                OnJoined?.Invoke(ID, PeerIDs);
            }
            _present.Clear();
            foreach (var id in _players())
            {
                if (id == me) continue;
                _present.Add(id);
                if (_peerOf.ContainsKey(id)) continue;
                int peer = _next++;
                _peerOf[id] = peer; PeerIDs.Add(peer);
                OnPeerJoined?.Invoke(peer);
            }
            _gone.Clear();
            foreach (var kv in _peerOf) if (!_present.Contains(kv.Key)) _gone.Add(kv.Key);
            foreach (var id in _gone)
            {
                int peer = _peerOf[id];
                _peerOf.Remove(id); PeerIDs.Remove(peer);
                OnPeerLeft?.Invoke(peer);
            }
        }

        /// <summary>The Nakama user id behind a UniVoice peer id (null if unknown): proximity needs to know whose voice this is.</summary>
        public string UserOf(int peer)
        {
            foreach (var kv in _peerOf) if (kv.Value == peer) return kv.Key;
            return null;
        }

        public void SendAudioFrame(AudioFrame frame)
        {
            if (YourVoiceSettings.muteAll) return;
            var packet = VoicePacket.Write(frame.timestamp, frame.frequency, frame.channelCount, frame.samples);
            if (packet != null) _transport.SendVoice(packet);
        }

        private void OnPacket(string userId, byte[] packet)
        {
            if (YourVoiceSettings.deafenAll) return;
            if (!_peerOf.TryGetValue(userId, out int peer)) return;                        // not (yet) in the boat as far as we know: ignored
            if (YourVoiceSettings.deafenedPeers.Contains(peer)) return;
            if (!VoicePacket.TryRead(packet, out long ts, out int frequency, out int channels, out byte[] payload)) return;
            OnReceivedPeerAudioFrame?.Invoke(peer, new AudioFrame { timestamp = ts, frequency = frequency, channelCount = channels, samples = payload });
        }

        public void SubmitVoiceSettings() { }                                               // the settings only matter here (no server-side filtering yet)
        public void UpdateVoiceSettings(Action<VoiceSettings> modification) { modification?.Invoke(YourVoiceSettings); }

        public void Dispose()
        {
            _transport.VoiceReceived -= OnPacket;
            if (_joined) { _joined = false; OnLeft?.Invoke(); }
            _peerOf.Clear(); PeerIDs.Clear();
        }
    }
}
