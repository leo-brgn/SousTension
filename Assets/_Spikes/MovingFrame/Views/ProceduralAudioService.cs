using UnityEngine;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// The boat's sound with Unity's own audio, no middleware: the loops are synthesised once at start (hums, whistle, water, creaks, six alarm patterns), so the
    /// game is audible without any sound file; real recordings (free library) can replace a clip later without touching the mix. Volumes and pitches glide
    /// towards the target so a change is never a click.
    /// </summary>
    public sealed class ProceduralAudioService : MonoBehaviour, IAudioService
    {
        private const int Rate = 22050;
        private const float MasterVolume = 0.6f;
        private AudioSource _reactor, _turbine, _grid, _water, _hull, _alarm;
        private AudioMix _target = AudioMix.Silent;
        private AlarmTone _alarmTone = AlarmTone.None;
        private AudioClip[] _alarmClips;
        private AudioClip[] _cues;
        private AudioSource _oneShot;
        private float _alarmVolume;

        public void Apply(AudioMix mix) { _target = mix; }

        public void Play(AudioCue cue)
        {
            var clip = _cues[(int)cue.Kind];
            if (clip == null || cue.Gain < 0.02f) return;
            _oneShot.pitch = cue.Kind == CueKind.Footstep ? Random.Range(0.85f, 1.15f) : 1f;      // cosmetic variation, not simulation
            _oneShot.PlayOneShot(clip, cue.Gain * MasterVolume * (cue.Kind == CueKind.Footstep ? 0.5f : 1f));
        }

        private void Awake()
        {
            var rng = new System.Random(7);                                                  // sound design noise, not simulation: any fixed seed will do
            _reactor = Loop("Reactor", Tones(2f, (55f, 0.6f), (110f, 0.3f), (165f, 0.1f)));
            _turbine = Loop("Turbine", Tones(2f, (1200f, 0.25f), (1800f, 0.1f), (600f, 0.15f)));
            _grid = Loop("Grid", Tones(2f, (50f, 0.5f), (100f, 0.3f), (150f, 0.1f)));
            _water = Loop("Water", Water(4f, rng));
            _hull = Loop("Hull", Creaks(6f, rng));
            _alarm = Loop("Alarm", null);
            _oneShot = Loop("OneShots", null);
            _oneShot.loop = false;
            _cues = new AudioClip[6];
            _cues[(int)CueKind.Hatch] = Thump(0.45f, 90f, 0.9f, 0.35f, rng);                          // a heavy wheel and a bolt: low thump with a rattle
            _cues[(int)CueKind.Breaker] = Thump(0.12f, 400f, 0.5f, 0.6f, rng);                        // a relay click
            _cues[(int)CueKind.Pump] = Thump(0.35f, 140f, 0.8f, 0.15f, rng);                          // a motor catching or stopping
            _cues[(int)CueKind.Telegraph] = Bell(1.4f, 660f);                                         // the engine room bell
            _cues[(int)CueKind.Footstep] = Thump(0.14f, 180f, 0.45f, 0.7f, rng);                      // a boot on a metal grating
            _cues[(int)CueKind.Scram] = Thump(1.2f, 38f, 1f, 0.1f, rng);                              // the rods falling
            _alarmClips = new AudioClip[7];
            _alarmClips[(int)AlarmTone.Reactor] = Pattern(3f, new[] { (0f, 110f, 1.2f, 0.9f) });                                                   // gong: one low note, a pause
            _alarmClips[(int)AlarmTone.Power] = Pattern(2f, new[] { (0f, 220f, 0.12f, 0.8f), (0.25f, 220f, 0.12f, 0.8f), (0.5f, 220f, 0.12f, 0.8f) }); // three short buzzes
            _alarmClips[(int)AlarmTone.Water] = Pattern(3f, new[] { (0f, 330f, 0.4f, 0.5f), (0.35f, 392f, 0.5f, 0.6f), (0.7f, 440f, 0.8f, 0.7f) });   // muffled bell, rising
            _alarmClips[(int)AlarmTone.Flood] = Pattern(2f, new[] { (0f, 440f, 0.5f, 0.9f), (0.5f, 554f, 0.5f, 0.9f), (1f, 440f, 0.5f, 0.9f), (1.5f, 554f, 0.5f, 0.9f) }); // two-tone siren
            _alarmClips[(int)AlarmTone.ReactorCritical] = Pattern(1f, new[] { (0f, 300f, 0.2f, 1f), (0.25f, 400f, 0.2f, 1f), (0.5f, 300f, 0.2f, 1f), (0.75f, 400f, 0.2f, 1f) }); // klaxon
            _alarmClips[(int)AlarmTone.DarkGrid] = Pattern(4f, new[] { (0f, 45f, 1.6f, 1f) });                                                     // one long, deep blow, repeated
        }

        private AudioSource Loop(string name, AudioClip clip)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.clip = clip; source.loop = true; source.spatialBlend = 0f; source.volume = 0f; source.playOnAwake = false;
            if (clip != null) source.Play();
            return source;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float k = 1f - Mathf.Exp(-4f * dt);                                              // glide: about a quarter of a second
            Glide(_reactor, _target.ReactorVolume, _target.ReactorPitch, k);
            Glide(_turbine, _target.TurbineVolume, _target.TurbinePitch, k);
            Glide(_grid, _target.GridVolume, _target.GridPitch, k);
            Glide(_water, _target.WaterVolume * 0.7f, 1f, k);
            Glide(_hull, _target.HullVolume, 1f, k);
            if (_target.Alarm != _alarmTone)
            {
                _alarmVolume = Mathf.Lerp(_alarmVolume, 0f, 1f - Mathf.Exp(-12f * dt));      // fade the old alarm out, then switch
                if (_alarmVolume < 0.02f)
                {
                    _alarmTone = _target.Alarm;
                    _alarm.Stop();
                    _alarm.clip = _alarmTone == AlarmTone.None ? null : _alarmClips[(int)_alarmTone];
                    if (_alarm.clip != null) _alarm.Play();
                }
            }
            else _alarmVolume = Mathf.Lerp(_alarmVolume, _alarmTone == AlarmTone.None ? 0f : 0.7f * _target.AlarmGain, 1f - Mathf.Exp(-8f * dt));
            _alarm.volume = _alarmVolume * MasterVolume;
        }

        private static void Glide(AudioSource source, float volume, float pitch, float k)
        {
            source.volume = Mathf.Lerp(source.volume, volume * MasterVolume, k);
            source.pitch = Mathf.Lerp(source.pitch, pitch, k);
        }

        // ---- synthesis ----------------------------------------------------------------------------------------------------------------------

        private static AudioClip Make(string name, float[] samples)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, Rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        // A loop of sine partials with whole numbers of cycles per clip (no click at the seam) and a slow tremolo.
        private static AudioClip Tones(float seconds, params (float freq, float gain)[] partials)
        {
            int n = (int)(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, v = 0f;
                foreach (var (freq, gain) in partials) v += gain * Mathf.Sin(2f * Mathf.PI * Mathf.Round(freq * seconds) / seconds * t);
                data[i] = v * (0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * t / seconds)) * 0.5f;
            }
            return Make("tones", data);
        }

        // Low-passed noise with a slow swell: running water.
        private static AudioClip Water(float seconds, System.Random rng)
        {
            int n = (int)(seconds * Rate);
            var data = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                lp += 0.08f * ((float)(rng.NextDouble() * 2 - 1) - lp);
                float swell = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * i / n * 2f);
                data[i] = lp * swell * 1.6f;
            }
            Fade(data);
            return Make("water", data);
        }

        // Sparse creaks: a few short falling tones in a long quiet clip.
        private static AudioClip Creaks(float seconds, System.Random rng)
        {
            int n = (int)(seconds * Rate);
            var data = new float[n];
            int len = (int)(0.5f * Rate);
            for (int c = 0; c < 4; c++)
            {
                int start = (int)(rng.NextDouble() * (n - len));
                float freq = 70f + (float)rng.NextDouble() * 60f;
                for (int i = 0; i < len; i++)
                {
                    float env = Mathf.Sin(Mathf.PI * i / len);
                    data[start + i] += env * 0.5f * Mathf.Sin(2f * Mathf.PI * (freq - 25f * i / len) * i / Rate);
                }
            }
            Fade(data);
            return Make("hull", data);
        }

        // Notes (start s, frequency Hz, length s, gain) in a looping bar of `seconds`.
        private static AudioClip Pattern(float seconds, (float start, float freq, float len, float gain)[] notes)
        {
            int n = (int)(seconds * Rate);
            var data = new float[n];
            foreach (var (start, freq, len, gain) in notes)
            {
                int i0 = (int)(start * Rate), count = (int)(len * Rate);
                for (int i = 0; i < count && i0 + i < n; i++)
                {
                    float t = i / (float)Rate, env = Mathf.Min(1f, i / (0.01f * Rate)) * Mathf.Exp(-3f * i / count);
                    float wave = Mathf.Sin(2f * Mathf.PI * freq * t) + 0.4f * Mathf.Sin(2f * Mathf.PI * freq * 2f * t);
                    data[i0 + i] += wave * env * gain * 0.4f;
                }
            }
            return Make("alarm", data);
        }

        // A falling tone with a burst of noise: thumps, clicks, steps. noise = share of noise in the attack.
        private static AudioClip Thump(float seconds, float freq, float gain, float noise, System.Random rng)
        {
            int n = (int)(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, env = Mathf.Min(1f, i / (0.004f * Rate)) * Mathf.Exp(-6f * i / n);
                float tone = Mathf.Sin(2f * Mathf.PI * freq * (1f - 0.4f * i / n) * t);
                float hiss = (float)(rng.NextDouble() * 2 - 1) * Mathf.Exp(-30f * i / n);
                data[i] = (tone * (1f - noise) + hiss * noise) * env * gain;
            }
            return Make("cue", data);
        }

        private static AudioClip Bell(float seconds, float freq)
        {
            int n = (int)(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, env = Mathf.Exp(-4f * i / n);
                data[i] = (Mathf.Sin(2f * Mathf.PI * freq * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * freq * 2.76f * t) + 0.25f * Mathf.Sin(2f * Mathf.PI * freq * 5.4f * t)) * env * 0.4f;
            }
            return Make("bell", data);
        }

        private static void Fade(float[] data)
        {
            int fade = 400;
            for (int i = 0; i < fade; i++)
            {
                float g = i / (float)fade;
                data[i] *= g; data[data.Length - 1 - i] *= g;
            }
        }
    }
}
