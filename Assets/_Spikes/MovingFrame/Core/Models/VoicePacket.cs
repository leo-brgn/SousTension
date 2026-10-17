using System;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// The bytes of one voice packet on the wire (E8-07c): timestamp (8), frequency (4), channels (4), then the Opus payload. Big endian, no allocation beyond the
    /// result. The sender is not in the packet: the server tells who sent it (the match presence), so a client cannot speak in someone else's name.
    /// </summary>
    public static class VoicePacket
    {
        public const int HeaderBytes = 16;
        public const int MaxPayloadBytes = 1200;               // one datagram: an Opus speech frame is far smaller

        public static byte[] Write(long timestamp, int frequency, int channels, byte[] payload)
        {
            if (payload == null || payload.Length == 0 || payload.Length > MaxPayloadBytes) return null;
            var bytes = new byte[HeaderBytes + payload.Length];
            PutLong(bytes, 0, timestamp); PutInt(bytes, 8, frequency); PutInt(bytes, 12, channels);
            Buffer.BlockCopy(payload, 0, bytes, HeaderBytes, payload.Length);
            return bytes;
        }

        /// <summary>False for a packet that is too short, too long or has an impossible header: it is dropped, never trusted.</summary>
        public static bool TryRead(byte[] packet, out long timestamp, out int frequency, out int channels, out byte[] payload)
        {
            timestamp = 0; frequency = 0; channels = 0; payload = null;
            if (packet == null || packet.Length <= HeaderBytes || packet.Length > HeaderBytes + MaxPayloadBytes) return false;
            timestamp = GetLong(packet, 0); frequency = GetInt(packet, 8); channels = GetInt(packet, 12);
            if (frequency < 8000 || frequency > 96000 || channels < 1 || channels > 2) return false;
            payload = new byte[packet.Length - HeaderBytes];
            Buffer.BlockCopy(packet, HeaderBytes, payload, 0, payload.Length);
            return true;
        }

        private static void PutInt(byte[] b, int at, int v) { b[at] = (byte)(v >> 24); b[at + 1] = (byte)(v >> 16); b[at + 2] = (byte)(v >> 8); b[at + 3] = (byte)v; }
        private static void PutLong(byte[] b, int at, long v) { PutInt(b, at, (int)(v >> 32)); PutInt(b, at + 4, (int)v); }
        private static int GetInt(byte[] b, int at) => (b[at] << 24) | (b[at + 1] << 16) | (b[at + 2] << 8) | b[at + 3];
        private static long GetLong(byte[] b, int at) => ((long)GetInt(b, at) << 32) | (uint)GetInt(b, at + 4);
    }
}
