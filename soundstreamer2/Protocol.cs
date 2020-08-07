using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace soundstreamer2
{
    enum CompressionType : byte
    {
        None = 0, Deflate = 1, Mp3 = 2, Ogg = 3
    }
    static class Protocol
    {
        public const ushort VERSION = 0;
        public enum MessageType : ushort
        {
            Ping = 1,
            Mute = 2,
            Unmute = 3,
        }
        #region client
        public static (ushort version, int sampleRate, byte channels, byte compressionType) ReceiveHeader(this NetworkIO client)
        {
            var r = client.Reader;
            if (new String(r.ReadChars(9)) != "SNDSTREAM") throw new ProtocolViolationException("invalid header");
            return (r.ReadUInt16(), r.ReadInt32(), r.ReadByte(), r.ReadByte());
        }
        public static void SendVersion(this NetworkIO client)
        {
            var w = client.Writer;
            w.Write("SNDSTREAM".ToArray());
            w.Write(VERSION);
        }
        public static ushort ReceiveResult(this NetworkIO client) => client.Reader.ReadUInt16();
        public static void SendPing(this NetworkIO client) => client.Writer.Write((ushort)MessageType.Ping);
        public static void SendMute(this NetworkIO client) => client.Writer.Write((ushort)MessageType.Mute);
        public static void SendUnmute(this NetworkIO client) => client.Writer.Write((ushort)MessageType.Unmute);
        #endregion
        #region server
        public static void SendHeader(this NetworkIO client, int sampleRate, byte channels, byte compressionType)
        {
            var w = client.Writer;
            w.Write("SNDSTREAM".ToArray());
            w.Write(VERSION);
            w.Write(sampleRate);
            w.Write(channels);
            w.Write(compressionType);
        }
        public static ushort ReceiveVersion(this NetworkIO client)
        {
            var r = client.Reader;
            if (new String(r.ReadChars(9)) != "SNDSTREAM") throw new ProtocolViolationException("invalid header");
            return r.ReadUInt16();
        }
        public static void SendResult(this NetworkIO client, ushort result) => client.Writer.Write(result);
        public static MessageType ReceiveMessageType(this NetworkIO client) => (MessageType)client.Reader.ReadUInt16();
        #endregion
    }
    class NetworkIO : IDisposable
    {
        static uint SId = 0;
        public NetworkIO(TcpClient client)
        {
            try
            {
                Client = client;
                Stream = Client.GetStream();
                Reader = new BinaryReader(Stream);
                Writer = new BinaryWriter(Stream);
            }
            catch
            {
                Dispose();
                throw;
            }
            Id = SId++;
        }
        public TcpClient Client { get; }
        public NetworkStream Stream { get; }
        public BinaryReader Reader { get; }
        public BinaryWriter Writer { get; }
        public uint Id { get; }
        public bool Muted { get; set; } = true;

        public void Dispose()
        {
            Client?.Close();
            Stream?.Dispose();
            Reader?.Dispose();
            Writer?.Dispose();
        }
    }

}
