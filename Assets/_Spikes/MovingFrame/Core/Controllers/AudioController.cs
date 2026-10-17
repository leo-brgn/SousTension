using System.Collections.Generic;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>Every frame, tells the audio service what the boat should sound like (<see cref="AudioMix"/>) and which short sounds to play (<see cref="AudioCueDetector"/>).</summary>
    public sealed class AudioController : ITickable
    {
        private readonly MovingFrameModel _model;
        private readonly IAudioService _audio;
        private readonly AudioCueDetector _cues = new AudioCueDetector();
        private readonly List<AudioCue> _pending = new List<AudioCue>();

        public AudioController(MovingFrameModel model, IAudioService audio) { _model = model; _audio = audio; }

        public void Tick(float deltaTime)
        {
            var mix = AudioMix.Compute(_model);
            _audio.Apply(mix);
            _pending.Clear();
            _cues.Detect(_model, _pending);                                           // always: it must keep learning the state
            bool running = !_model.Mission.Valid || _model.Mission.Phase == "run";
            if (running) foreach (var cue in _pending) _audio.Play(cue);                // nothing plays behind the lobby, briefing and debrief screens
        }
    }
}
