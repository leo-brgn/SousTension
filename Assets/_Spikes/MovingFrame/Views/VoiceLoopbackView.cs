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
    /// Local test of the voice chain (E8-07b): microphone -> Opus encode -> Opus decode -> speaker, with the size of what would travel logged every few seconds.
    /// Started with <c>--voice-loopback</c>. Wear headphones (a speaker would feed back into the microphone). This is the same chain the Nakama transport will use,
    /// with the network between the two filters (E8-07c).
    /// </summary>
    public sealed class VoiceLoopbackView : MonoBehaviour
    {
        private UniMicInput _input;
        private StreamedAudioSourceOutput _output;
        private ConcentusEncodeFilter _encode;
        private ConcentusDecodeFilter _decode;
        private long _frames, _bytes;
        private float _nextLog;

        public static bool Requested => Array.IndexOf(Environment.GetCommandLineArgs(), "--voice-loopback") >= 0;

        private void Start()
        {
            var devices = Mic.AvailableDevices;
            if (devices.Count == 0) { Debug.LogWarning("[Voice] no microphone found"); enabled = false; return; }
            var device = devices[0];
            _encode = new ConcentusEncodeFilter(ConcentusFrequencies.Frequency_16000, encoderBitrate: 24000);           // speech: 24 kbit/s is about 60 bytes per 20 ms frame
            _decode = new ConcentusDecodeFilter();
            _output = StreamedAudioSourceOutput.New();
            _input = new UniMicInput(device);
            _input.OnFrameReady += OnFrame;
            device.StartRecording(20);
            Debug.Log("[Voice] loopback started on " + device.Name);
        }

        private void OnFrame(AudioFrame frame)
        {
            var packet = _encode.Run(frame);
            _frames++; _bytes += packet.samples.Length;
            _output.Feed(_decode.Run(packet));
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextLog || _frames == 0) return;
            _nextLog = Time.unscaledTime + 5f;
            Debug.Log($"[Voice] {_frames} frames, {(_bytes / (double)_frames):F0} B per frame, {(_bytes * 8 / Math.Max(Time.unscaledTime, 1f) / 1000):F1} kbit/s on average");
        }

        private void OnDestroy()
        {
            if (_input != null) { _input.OnFrameReady -= OnFrame; _input.Device?.StopRecording(); _input.Dispose(); }
            _output?.Dispose();
        }
    }
}
