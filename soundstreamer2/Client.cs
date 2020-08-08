using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using NAudio;
using NAudio.Wave;
using System.Net.Sockets;
using System.Threading;
using System.Globalization;

namespace soundstreamer2
{
    public class Client
    {
        public string Hostname { get; set; } = null;
        public ushort Port { get; set; } = 0;
        public int Id { get; set; } = 1;
        public string Code { get; set; } = null;

        NetworkIO nc;
        float Volume = 0.2f;
        bool Muted = false;
        FixOutDevice waveOut;
        WaveFormat waveFormat;
        Thread pingThread;
        Thread receiveThread;
        byte compression;

        public void Main()
        {
            var prevCode = "";
            if (File.Exists("clientcfg.txt"))
            {
                var f = File.ReadAllLines("clientcfg.txt");
                if (f[0] != Protocol.VERSION.ToString()) File.Delete("clientcfg.txt");
                else
                {
                    prevCode = f[1].ToUpperInvariant();
                    Volume = float.Parse(f[2], CultureInfo.InvariantCulture);
                }
            }
            try
            {
                if (Code == null)
                {
                    Console.Write("Enter connection code: ");
                    if (prevCode != "")
                    {
                        var prevcolor = Console.ForegroundColor;
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.Write(prevCode);
                        Console.ForegroundColor = prevcolor;
                        Console.CursorLeft -= prevCode.Length;
                        var line = Console.ReadLine();
                        if (line == "") Code = prevCode;
                        else Code = line;
                    }
                    else Code = Console.ReadLine();
                }
                if (Code == "")
                {
                    if (Hostname == null)
                    {
                        Code = "V" + Id;
                    }
                    else Code = "D" + Hostname + ":" + Port;
                }
                var (hostname, port) = ParseConnectionCode(Code.ToUpperInvariant());

                Console.WriteLine("Connecting");
                nc = new NetworkIO(new TcpClient(hostname, port));
                var (version, sampleRate, channels, compressionType) = nc.ReceiveHeader();
                compression = compressionType;
                if (compression >= (byte)CompressionType.Mp3) throw new NotImplementedException($"compression {(CompressionType)compression} not yet implemented");
                Console.CursorTop = 2;
                Console.WriteLine($"Samplerate: {sampleRate}, Channels: {channels}, Compression: {(CompressionType)compressionType}, Version: {version}");
                nc.SendVersion();
                var result = nc.ReceiveResult();
                if (result != 0) { Console.WriteLine($"Error {result}"); return; }

                if (sampleRate > 48000 || channels > 2)
                {
                    Console.WriteLine($"warning: samplerate ({sampleRate}) or channels ({channels}) is pretty high. press enter to continue");
                    Console.ReadLine();
                }

                waveFormat = new WaveFormat(sampleRate, channels);

                waveOut = new FixOutDevice(waveFormat);
                waveOut.Volume = Volume;
                waveOut.Init();

                pingThread = new Thread(() => PingLoop(nc));
                pingThread.Start();
                receiveThread = new Thread(() => ReceiveLoop(nc));
                receiveThread.Start();
                Console.Clear();
                while (nc.Client.Connected)
                {
                    Console.SetCursorPosition(0, 0);
                    File.WriteAllLines("clientcfg.txt", new[] { Protocol.VERSION.ToString(), Code, Volume.ToString(CultureInfo.InvariantCulture) });
                    Console.WriteLine($"Volume: {(Volume * 100).ToString("N0")}% {(Muted ? "(muted)" : "       ")}");
                    Console.WriteLine("Arrow keys to change volume, spacebar to mute");
                    Console.WriteLine("ctrl+arrows for fast change, - and = for slow change");
                    Console.WriteLine($"Samplerate: {sampleRate}, Channels: {channels}, Compression: {(CompressionType)compressionType}");
                    var key = Console.ReadKey(true);
                    switch (key.Key)
                    {
                        case ConsoleKey.OemMinus:
                        case ConsoleKey.Subtract:
                            if ((key.Modifiers & ConsoleModifiers.Control) == ConsoleModifiers.Control) Volume -= .02f;
                            else Volume -= .01f;

                            if (Volume <= 0f) Volume = 0f;
                            waveOut.Volume = Volume;
                            if (Volume < 0.005f && !Muted) goto case ConsoleKey.Spacebar;
                            break;
                        case ConsoleKey.OemPlus:
                        case ConsoleKey.Add:
                            if ((key.Modifiers & ConsoleModifiers.Control) == ConsoleModifiers.Control) Volume += .02f;
                            Volume += .01f;

                            if (Volume >= 1f) Volume = 1f;
                            waveOut.Volume = Volume;
                            if (Volume > 0.005f && Muted) goto case ConsoleKey.Spacebar;
                            break;
                        case ConsoleKey.DownArrow:
                            if ((key.Modifiers & ConsoleModifiers.Control) == ConsoleModifiers.Control) Volume -= .20f;
                            else Volume -= .10f;

                            if (Volume <= 0f) Volume = 0f;
                            waveOut.Volume = Volume;
                            if (Volume < 0.005f && !Muted) goto case ConsoleKey.Spacebar;
                            break;
                        case ConsoleKey.LeftArrow:
                            if ((key.Modifiers & ConsoleModifiers.Control) == ConsoleModifiers.Control) Volume -= .15f;
                            else Volume -= .05f;

                            if (Volume <= 0f) Volume = 0f;
                            waveOut.Volume = Volume;
                            if (Volume < 0.005f && !Muted) goto case ConsoleKey.Spacebar;
                            break;
                        case ConsoleKey.UpArrow:
                            if ((key.Modifiers & ConsoleModifiers.Control) == ConsoleModifiers.Control) Volume += .20f;
                            else Volume += .10f;

                            if (Volume >= 1f) Volume = 1f;
                            waveOut.Volume = Volume;
                            if (Volume > 0.005f && Muted) goto case ConsoleKey.Spacebar;
                            break;
                        case ConsoleKey.RightArrow:
                            if ((key.Modifiers & ConsoleModifiers.Control) == ConsoleModifiers.Control) Volume += .15f;
                            else Volume += .05f;

                            if (Volume >= 1f) Volume = 1f;
                            waveOut.Volume = Volume;
                            if (Volume > 0.005f && Muted) goto case ConsoleKey.Spacebar;
                            break;
                        case ConsoleKey.Spacebar:
                            if (Volume > 0.005f || !Muted)
                            {
                                Muted = !Muted;
                                if (Muted)
                                {
                                    waveOut.Volume = 0;
                                    nc.SendMute();
                                }
                                else
                                {
                                    waveOut.Volume = Volume;
                                    nc.SendUnmute();
                                }
                            }
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                Console.WriteLine("Exiting");
                waveOut?.Dispose();
                nc?.Dispose();
            }
        }
        void PingLoop(NetworkIO nc)
        {
            try
            {
                while (nc.Client.Connected)
                {
                    Thread.Sleep(3000);
                    nc.SendPing();
                }
            }
            catch { }
        }
        void ReceiveLoop(NetworkIO nc)
        {
            try
            {
                while (nc.Client.Connected)
                {
                    int len = nc.Reader.ReadInt32();
                    if (len > 67108864) throw new ArgumentOutOfRangeException("buffer too big!");//limit to 64 mb in case something fricks up and requests a 2 gb buffer
                    var buf = nc.Reader.ReadBytes(len);
                    try
                    {
                        //clear buffer if we're too far behind (there's probably a better way to do this)
                        if (waveOut.bufferedWave.BufferedDuration.TotalMilliseconds > 500d) waveOut.bufferedWave.ClearBuffer();

                        switch ((CompressionType)compression)
                        {
                            case CompressionType.None:
                                waveOut.bufferedWave.AddSamples(buf, 0, len);
                                break;
                            case CompressionType.Deflate:
                                using (var ms = new MemoryStream(buf))
                                using (var ds = new DeflateStream(ms, CompressionMode.Decompress, true))
                                using (var ms2 = new MemoryStream())
                                {
                                    ds.CopyTo(ms2);
                                    ds.Close();
                                    var arr = ms2.ToArray();
                                    waveOut.bufferedWave.AddSamples(arr, 0, arr.Length);
                                }
                                break;
                            case CompressionType.Mp3:
                            case CompressionType.Ogg:
                                Console.WriteLine("not implemented");
                                break;
                            default:
                                Console.WriteLine("unknown compression type");
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        public (string hostname, ushort port) ParseConnectionCode(string code)
        {
            string hostname; ushort port;
            if (code[0] == 'D')
            {
                var arg = code.Substring(1).Split(':');
                hostname = arg[0];
                port = ushort.Parse(arg[1]);
            }
            else if (code[0] == 'V')
            {
                hostname = "computernewb.com";
                var id = int.Parse(code.Substring(1));
                port = Program.VmPorts.First(x => x.Key.id == id).Value;
            }
            else
            {
                string region;
                switch (code[0])
                {
                    case 'U': region = "us"; break;
                    case 'E': region = "eu"; break;
                    case 'A': region = "au"; break;
                    case 'P': region = "ap"; break;
                    default: throw new ArgumentException("invalid code");
                }
                char sub = code[1];
                port = ushort.Parse(code.Substring(2));
                if (region != "us") hostname = $"{sub}.tcp.{region}.ngrok.io";
                else hostname = $"{sub}.tcp.ngrok.io";
            }

            return (hostname, port);
        }
    }
}
