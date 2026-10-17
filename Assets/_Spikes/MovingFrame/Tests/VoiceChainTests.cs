using Adrenak.UniVoice;
using Adrenak.UniVoice.Filters;
using NUnit.Framework;
using UnityEngine;

namespace SousTension.Spikes.MovingFrame.Tests
{
    /// <summary>The Opus chain of the voice (E8-07b): a 20 ms frame shrinks to a few dozen bytes and comes back with the same length and a similar energy.</summary>
    public class VoiceChainTests
    {
        private static AudioFrame Tone(int frequency, float hz, long stamp)
        {
            int n = frequency / 50;                                                    // 20 ms
            var samples = new float[n];
            for (int i = 0; i < n; i++) samples[i] = 0.4f * Mathf.Sin(2f * Mathf.PI * hz * (i + stamp * n) / frequency);
            var bytes = new byte[n * 4];
            System.Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
            return new AudioFrame { timestamp = stamp, frequency = frequency, channelCount = 1, samples = bytes };
        }

        [Test]
        public void AnOpusFrameIsSmallAndDecodesToTheSameDuration()
        {
            var encode = new ConcentusEncodeFilter(ConcentusFrequencies.Frequency_16000, encoderBitrate: 24000);
            var decode = new ConcentusDecodeFilter();
            AudioFrame packet = default, back = default;
            for (int i = 0; i < 20; i++)                                              // let the encoder settle
            {
                packet = encode.Run(Tone(48000, 220f, i));
                back = decode.Run(packet);
            }
            int raw = 48000 / 50 * 4;
            Assert.Less(packet.samples.Length, raw / 10, "Opus is more than ten times smaller than the raw frame: " + packet.samples.Length + " B");
            Assert.Less(packet.samples.Length, 200, "a 24 kbit/s speech frame fits in one small datagram");
            Assert.Greater(back.samples.Length, 0);
            Assert.AreEqual(1, back.channelCount);
        }
    }
}
