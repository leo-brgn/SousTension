using System;
using System.Collections.Generic;
using Adrenak.UniVoice;
using NUnit.Framework;

namespace SousTension.Spikes.MovingFrame.Tests
{
    internal sealed class FakeVoiceTransport : IVoiceTransport
    {
        public readonly List<byte[]> Sent = new List<byte[]>();
        public event Action<string, byte[]> VoiceReceived;
        public void SendVoice(byte[] packet) { Sent.Add(packet); }
        public void Receive(string from, byte[] packet) { VoiceReceived?.Invoke(from, packet); }
    }

    /// <summary>The voice packet format and the UniVoice client of the match (E8-07c), with a fake transport: no network, no microphone.</summary>
    public class VoiceTransportTests
    {
        [Test]
        public void APacketKeepsItsFieldsAndRefusesNonsense()
        {
            var payload = new byte[] { 1, 2, 3, 4, 5 };
            var packet = VoicePacket.Write(1234567890123L, 16000, 1, payload);
            Assert.AreEqual(VoicePacket.HeaderBytes + payload.Length, packet.Length);
            Assert.IsTrue(VoicePacket.TryRead(packet, out long ts, out int freq, out int channels, out byte[] back));
            Assert.AreEqual(1234567890123L, ts); Assert.AreEqual(16000, freq); Assert.AreEqual(1, channels);
            CollectionAssert.AreEqual(payload, back);

            Assert.IsNull(VoicePacket.Write(0, 16000, 1, new byte[0]), "an empty frame is not sent");
            Assert.IsNull(VoicePacket.Write(0, 16000, 1, new byte[VoicePacket.MaxPayloadBytes + 1]), "an oversized frame is not sent");
            Assert.IsFalse(VoicePacket.TryRead(new byte[5], out _, out _, out _, out _), "too short");
            Assert.IsFalse(VoicePacket.TryRead(new byte[VoicePacket.HeaderBytes + VoicePacket.MaxPayloadBytes + 1], out _, out _, out _, out _), "too long");
            var bad = VoicePacket.Write(0, 16000, 1, payload); bad[15] = 9;                         // 9 channels
            Assert.IsFalse(VoicePacket.TryRead(bad, out _, out _, out _, out _), "an impossible header is dropped");
            Assert.IsFalse(VoicePacket.TryRead(null, out _, out _, out _, out _));
        }

        [Test]
        public void TheClientFollowsThePlayers_AndRoutesPacketsToTheirPeer()
        {
            var transport = new FakeVoiceTransport();
            var players = new List<string> { "me", "ann" };
            var client = new MatchVoiceClient(transport, () => "me", () => players);
            var joined = new List<int>(); var left = new List<int>(); var heard = new List<(int, AudioFrame)>();
            int joinedSelf = 0;
            client.OnJoined += (id, peers) => joinedSelf++;
            client.OnPeerJoined += joined.Add; client.OnPeerLeft += left.Add;
            client.OnReceivedPeerAudioFrame += (peer, frame) => heard.Add((peer, frame));

            client.Tick();
            Assert.AreEqual(1, joinedSelf);
            CollectionAssert.AreEqual(new[] { 1 }, joined, "the other sailor is peer 1");
            Assert.AreEqual("ann", client.UserOf(1));

            var packet = VoicePacket.Write(7, 16000, 1, new byte[] { 9, 9 });
            transport.Receive("ann", packet);
            Assert.AreEqual(1, heard.Count); Assert.AreEqual(1, heard[0].Item1);
            Assert.AreEqual(16000, heard[0].Item2.frequency); Assert.AreEqual(2, heard[0].Item2.samples.Length);
            transport.Receive("stranger", packet);
            Assert.AreEqual(1, heard.Count, "a packet from someone who is not in the boat is ignored");
            transport.Receive("ann", new byte[3]);
            Assert.AreEqual(1, heard.Count, "a garbage packet is ignored");

            players.Add("bob"); client.Tick();
            CollectionAssert.AreEqual(new[] { 1, 2 }, joined);
            players.Remove("ann"); client.Tick();
            CollectionAssert.AreEqual(new[] { 1 }, left);
            CollectionAssert.AreEqual(new[] { 2 }, client.PeerIDs);
            transport.Receive("ann", packet);
            Assert.AreEqual(1, heard.Count, "a sailor who left is no longer heard");
        }

        [Test]
        public void TheClientSendsOpusFrames_AndHonoursMuteAndDeafen()
        {
            var transport = new FakeVoiceTransport();
            var client = new MatchVoiceClient(transport, () => "me", () => new[] { "me", "ann" });
            client.Tick();
            var frame = new AudioFrame { timestamp = 5, frequency = 16000, channelCount = 1, samples = new byte[] { 1, 2, 3 } };
            client.SendAudioFrame(frame);
            Assert.AreEqual(1, transport.Sent.Count);
            client.UpdateVoiceSettings(s => s.muteAll = true);
            client.SendAudioFrame(frame);
            Assert.AreEqual(1, transport.Sent.Count, "muted: nothing leaves");

            int heard = 0;
            client.OnReceivedPeerAudioFrame += (p, f) => heard++;
            var packet = VoicePacket.Write(1, 16000, 1, new byte[] { 1 });
            client.UpdateVoiceSettings(s => s.deafenAll = true);
            transport.Receive("ann", packet);
            Assert.AreEqual(0, heard, "deafened: nothing is heard");
            client.UpdateVoiceSettings(s => { s.deafenAll = false; s.SetDeaf(1, true); });
            transport.Receive("ann", packet);
            Assert.AreEqual(0, heard, "that peer is deafened");
            client.UpdateVoiceSettings(s => s.SetDeaf(1, false));
            transport.Receive("ann", packet);
            Assert.AreEqual(1, heard);
        }
    }
}
