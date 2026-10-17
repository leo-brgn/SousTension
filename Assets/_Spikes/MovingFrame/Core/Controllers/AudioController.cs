namespace SousTension.Spikes.MovingFrame
{
    /// <summary>Every frame, tells the audio service what the boat should sound like (computed from the model by <see cref="AudioMix"/>).</summary>
    public sealed class AudioController : ITickable
    {
        private readonly MovingFrameModel _model;
        private readonly IAudioService _audio;

        public AudioController(MovingFrameModel model, IAudioService audio) { _model = model; _audio = audio; }

        public void Tick(float deltaTime) => _audio.Apply(AudioMix.Compute(_model));
    }
}
