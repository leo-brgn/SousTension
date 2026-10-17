using System;
using Adrenak.UniMic;
using Adrenak.UniVoice;
using Adrenak.UniVoice.Filters;
using Adrenak.UniVoice.Inputs;
using Adrenak.UniVoice.Outputs;
using UnityEngine;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// The voice chat of the crew (E8-07c): microphone -> Opus -> the match -> the other players -> Opus -> one speaker per peer. UniVoice does the audio; the match relays
    /// the packets (<see cref="MatchVoiceClient"/>). Started with <c>--voice</c> while the proximity and the settings screen are not there yet (E8-07d): use headphones.
    /// </summary>
    public sealed class VoiceChatView : MonoBehaviour
    {
        private MatchVoiceClient _client;
        private ClientSession<int> _session;
        private UniMicInput _input;

        public static bool Requested => Array.IndexOf(Environment.GetCommandLineArgs(), "--voice") >= 0;
        public MatchVoiceClient Client => _client;

        public void Bind(IVoiceTransport transport, MovingFrameModel model)
        {
            var devices = Mic.AvailableDevices;
            if (devices.Count == 0) { Debug.LogWarning("[Voice] no microphone: the crew is heard, you are not"); }
            _client = new MatchVoiceClient(transport, () => model.LocalId, model.AllPlayerIds);
            Mic.Device device = devices.Count > 0 ? devices[0] : null;
            _input = new UniMicInput(device);
            _session = new ClientSession<int>(_client, _input, () => StreamedAudioSourceOutput.New());
            _session.InputFilters.Add(new ConcentusEncodeFilter(ConcentusFrequencies.Frequency_16000, encoderBitrate: 24000));   // ~60 bytes per 20 ms
            _session.AddOutputFilter<ConcentusDecodeFilter>(() => new ConcentusDecodeFilter());                                   // one decoder per peer (they are stateful)
            device?.StartRecording(20);
            Debug.Log("[Voice] chat started" + (device != null ? " on " + device.Name : ""));
        }

        private void Update() { _client?.Tick(); }

        private void OnDestroy()
        {
            _session?.Dispose();
        }
    }
}
