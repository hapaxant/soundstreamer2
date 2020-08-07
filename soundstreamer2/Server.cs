using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Security;
using System.Security.Cryptography;
using NAudio;
using NAudio.Wave;
using System.Collections.Concurrent;

namespace soundstreamer2
{
    public class Server
    {
        public string NgrokToken { private get; set; } = "1dGNOmzqVuNMYRRYqzjwmyipxZj_NopAzGK6qPcT5Ca6zQGc";
        //public const string Ngrok32Bit = "ftp://ftp.computernewb.com/ngrok/4VmDzA7iaHb/ngrok-stable-windows-386.zip";
        //public const string Ngrok64Bit = "ftp://ftp.computernewb.com/ngrok/4VmDzA7iaHb/ngrok-stable-windows-amd64.zip";
        public const string Ngrok32Bit = "https://bin.equinox.io/c/4VmDzA7iaHb/ngrok-stable-windows-386.zip";
        public const string Ngrok64Bit = "https://bin.equinox.io/c/4VmDzA7iaHb/ngrok-stable-windows-amd64.zip";
        public const string Ngrok32Sum = "957E1A163B93FE6A8CD50C70CB453618679CDDF5636739935B3AFBCA1D972744";
        public const string Ngrok64Sum = "C8D6C591AAA978D1078C5AC1DBB579A66EF994E6F3CA6F7C461D5FB8B8FB2DB0";
        public const string SketchyVac = "https://files.catbox.moe/8ya1iu.zip";
        public const string SketchyVacSum = "84982BFD90FFB67C387F9BA6D41096B502D56FF8D5234520EB0C9735C6D04ADF";

        public bool UseNgrok { get; set; } = true;
        public bool UseVac { get; set; } = true;
        public ushort Port { get; set; } = 0;
        public string NgrokRegion { get; set; } = "us";

        public static bool HasNgrok() => File.Exists("ngrok.exe");
        public void DownloadNgrok()
        {
            //try
            //{
            var ngrok = !Environment.Is64BitOperatingSystem ? Ngrok32Bit : Ngrok64Bit;
            var ngrokhash = !Environment.Is64BitOperatingSystem ? Ngrok32Sum : Ngrok64Sum;
            //    var wr = (FtpWebRequest)WebRequest.Create(ngrok);
            //    wr.Credentials = new NetworkCredential("anon", "anon");
            //    using (var ms = new MemoryStream())
            //    {
            //        using (var ws = wr.GetResponse())
            //        using (var rs = ws.GetResponseStream())
            //        {
            //            rs.CopyTo(ms);
            //        }
            //        ms.Position = 0;
            //        EnsureCorrectHash(ngrokhash, ms);
            //        using (var zs = ZipStorer.Open(ms, FileAccess.Read))
            //        using (var fs = new FileStream("ngrok.exe", FileMode.CreateNew, FileAccess.Write))
            //        {
            //            Trace.Assert(zs.ExtractFile(zs.ReadCentralDir().First(x => x.FilenameInZip == "ngrok.exe"), fs));
            //        }
            //    }
            //}
            //catch (WebException)
            //{
            //    Console.WriteLine("ftp seems to be down... download ngrok manually.");
            //    Process.Start("https://ngrok.com/download");
            //    Console.WriteLine("press enter once you put ngrok.exe in this directory");
            //    Console.ReadLine();
            //}
            DownloadRemoteFile(ngrok, "ngrok.zip");

            if (!File.Exists("ngrok.zip"))
            {
                Console.Error.WriteLine("ngrok.zip not found!");
                UseNgrok = false;
                return;
            }

            using (var ms = new MemoryStream(File.ReadAllBytes("ngrok.zip")))
            {
                EnsureCorrectHash(ngrokhash, ms);
                using (var zs = ZipStorer.Open(ms, FileAccess.Read))
                using (var fs = new FileStream("ngrok.exe", FileMode.CreateNew, FileAccess.Write))
                {
                    Trace.Assert(zs.ExtractFile(zs.ReadCentralDir().First(x => x.FilenameInZip == "ngrok.exe"), fs));
                }
            }

            if (!File.Exists("ngrok.exe"))
            {
                Console.Error.WriteLine("ngrok.exe not found!");
                UseNgrok = false;
            }
        }

        static void EnsureCorrectHash(string stringhash, MemoryStream ms)
        {
            var prevpos = ms.Position;
            ms.Position = 0;
            try
            {
                using (var sha256 = SHA256.Create())
                {
                    var hash = String.Concat(sha256.ComputeHash(ms).Select(x => x.ToString("X2")));
                    if (hash != stringhash) throw new SecurityException("file hashes do not match.");
                }
            }
            finally
            {
                ms.Position = prevpos;
            }
        }

        public static bool HasVac()
        {
            var dc = WaveIn.DeviceCount;
            for (int i = 0; i < dc; i++)
            {
                var wic = WaveIn.GetCapabilities(i);
                string productName = wic.ProductName;
                if (productName.Contains("Virtual Cable") || productName.Contains("Virtual Audio Cable") ||
                    productName.Contains("Stereo Mix")) return true;
            }
            return false;
        }
        public void InstallVac()
        {
            if (!File.Exists("sketchy_vac.zip"))
            {
                Console.WriteLine("vac zip does not exist, downloading");
                DownloadRemoteFile(SketchyVac, "sketchy_vac.zip");
            }
            if (!File.Exists("sketchy_vac.zip"))
            {
                Console.Error.WriteLine("sketchy_vac.zip not found!");
                UseVac = false;
                return;
            }
            Stack<string> files = new Stack<string>();
            using (var ms = new MemoryStream(File.ReadAllBytes("sketchy_vac.zip")))
            {
                EnsureCorrectHash(SketchyVacSum, ms);
                using (var zs = ZipStorer.Open(ms, FileAccess.Read))
                {
                    foreach (var item in zs.ReadCentralDir())
                    {
                        var path = Path.GetFullPath("vac/" + String.Join("/", item.FilenameInZip.Split('/').Skip(1)));
                        zs.ExtractFile(item, path);
                        files.Push(path);
                    }
                }
            }

            Process p;
            if (Environment.Is64BitOperatingSystem) p = Process.Start("vac\\setup64.exe");
            else p = Process.Start("vac\\setup.exe");
            p.WaitForExit();

            while (files.Count > 0)
            {
                var path = files.Pop();
                if (File.Exists(path)) File.Delete(path);
                else if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any()) Directory.Delete(path);
            }
        }

        private void DownloadRemoteFile(string url, string filename)
        {
            if (Environment.OSVersion.Version.Major > 5)//not windows xp
            {
                Process.Start(new ProcessStartInfo() { FileName = "downfile.exe", Arguments = $"\"{url}\" \"{filename}\"", UseShellExecute = false }).WaitForExit();
            }
            else
            {
                Console.WriteLine("windows xp sucks, please download this file manually");
                Process.Start(url);
                Console.WriteLine("press enter to continue");
                Console.ReadLine();
                var urlfname = url.Substring(url.LastIndexOf('/') + 1);
                if (File.Exists(urlfname)) File.Move(urlfname, filename);
            }
        }

        public static string StartNgrokTunnel(ushort port)
        {
            int tries = 0;
            while (true)
            {
                var wr = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:4040/api/tunnels");
                wr.Method = "POST";
                wr.ContentType = "application/json";
                using (var requeststream = wr.GetRequestStream())
                using (var requestwriter = new StreamWriter(requeststream))
                {
                    requestwriter.Write($@"{{""addr"":""{port}"",""proto"":""tcp"",""name"":""cbt""}}");
                }
                try
                {
                    using (var response = wr.GetResponse())
                    using (var responsestream = response.GetResponseStream())
                    using (var responsereader = new StreamReader(responsestream))
                    {
                        return (Newtonsoft.Json.JsonConvert.DeserializeObject(responsereader.ReadToEnd()) as dynamic).public_url.ToString();
                    }
                }
                //catch (WebException ex)
                catch (WebException)
                {
                    //Console.WriteLine(ex.Status);
                    //using (var r = ex.Response)
                    //using (var rs = r.GetResponseStream())
                    //using (var rsr = new StreamReader(rs))
                    //{
                    //    Console.WriteLine(rsr.ReadToEnd());
                    //}
                    Console.Write(".");
                    if (++tries % 200 == 0)
                    {
                        const string TXT = "that's a lot of waiting. try installing a vpn.";
                        var l = Console.BufferWidth - TXT.Length;
                        if (Console.CursorLeft > l) Console.CursorLeft = l;
                        Console.Write(TXT);
                    }
                    Thread.Sleep(50);
                    continue;
                }
            }
        }

        string NgrokUrl = null;
        public void Main()
        {
            //if (!File.Exists("sketchy_vac.zip")) UseVac = false;
            if (UseNgrok && Port == 0) Port = 6969;
            if (Port == 0)
            {
                if (Program.VmPorts.Keys.Select(x => x.machinename).Contains(Environment.MachineName)) Port = Program.VmPorts.First(x => x.Key.machinename == Environment.MachineName).Value;
                else throw new ArgumentException("port is not set and no default port for this machine.");
            }

            if (UseVac && !HasVac()) InstallVac();
            if (UseNgrok && !HasNgrok()) DownloadNgrok();

            Process ngrok = null;
            if (UseNgrok)
            {
                //Process.Start(new ProcessStartInfo("ngrok.exe", "authtoken " + NgrokToken) { UseShellExecute = false }).WaitForExit();
                //can't use above code bcs ngrok screams in windows xp
                var tokenpath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ngrok2", "ngrok.yml");
                (new FileInfo(tokenpath).Directory).Create();
                File.WriteAllText(tokenpath, "authtoken: " + NgrokToken);

                ngrok = new Process() { StartInfo = new ProcessStartInfo("ngrok.exe", $"start --none -region={NgrokRegion}") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardInput = true } };
                ngrok.Start();

                NgrokUrl = StartNgrokTunnel(Port);
                Console.WriteLine(NgrokUrl);
            }

            InitNAudio();

            var code = GetConnectCode();
            Console.Title = $"Connect with code '{code}'!";
            Console.WriteLine("Use this code to connect with client: " + code);

            ServerLoop();
        }

        private string GetConnectCode()
        {
            StringBuilder sb = new StringBuilder();
            string port;
            if (UseNgrok)
            {
                var parts = NgrokUrl.Substring("tcp://".Length).Split('.');
                port = parts[parts.Length - 1].Substring("io:".Length);
                string region = parts.Length == 4 ? region = "us" : parts[2];
                switch (region)
                {
                    case "au": sb.Append('A'); break;
                    case "ap": sb.Append('P'); break;
                    case "eu": sb.Append('E'); break;
                    default: sb.Append('U'); break;
                }
                sb.Append(parts[0]);
                sb.Append(port);
            }
            else
            {
                if (Program.VmPorts.Keys.Select(x => x.machinename).Contains(Environment.MachineName))
                {
                    sb.Append('V');
                    sb.Append(Program.VmPorts.Keys.First(x => x.machinename == Environment.MachineName).id);
                }
                else
                {
                    sb.Append('D');
                    string ip;
#if DEBUG
                    ip = "localhost";
#else
                    var client = (HttpWebRequest)WebRequest.Create("http://api.ipify.org");
                    using (var r = client.GetResponse())
                    using (var rs = r.GetResponseStream())
                    using (var rsr = new StreamReader(rs))
                    {
                        ip = rsr.ReadToEnd();
                    }
#endif
                    sb.Append(ip);
                    sb.Append(':');
                    port = Port.ToString();
                    sb.Append(port);
                }
            }
            return sb.ToString();
        }

        WaveInEvent waveIn;
        WaveFormat waveFormat;
        public int BufferMs = 50;
        public int SampleRate { get; set; } = 22050;
        public byte Channels { get; set; } = 1;
        private byte _compressionType = (byte)CompressionType.Deflate;
        public byte Compression
        {
            get => _compressionType;
            set
            {
                if (value < 0 && value > 3) throw new ArgumentOutOfRangeException($"compression type {value} not supported");
                _compressionType = value;
            }
        }
        public TcpListener tcpListener;
        private void InitNAudio()
        {
            waveFormat = new WaveFormat(SampleRate, Channels);
            waveIn = new WaveInEvent()
            {
                BufferMilliseconds = BufferMs,
                NumberOfBuffers = 3,
                WaveFormat = waveFormat,
            };
            waveIn.DataAvailable += WaveIn_DataAvailable;
            waveIn.StartRecording();
        }

        private void WaveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            try
            {
                byte[] buf;
                int len;
                if (connectedClients.Count == 0) return;
                switch ((CompressionType)Compression)
                {
                    case CompressionType.None:
                        buf = e.Buffer;
                        len = e.BytesRecorded;
                        break;
                    case CompressionType.Deflate:
                        using (var ms = new MemoryStream())
                        using (var ds = new DeflateStream(ms, CompressionMode.Compress, true))
                        {
                            ds.Write(e.Buffer, 0, e.BytesRecorded);
                            ds.Close();//need to close otherwise memorystream will be 0 bytes??? ok bud (ds.Flush() did NOT work)
                            buf = ms.ToArray();
                            len = buf.Length;
                        }
                        //Console.WriteLine($"({(e.BytesRecorded - len > 0 ? "+" : "")}{e.BytesRecorded - len}) old {e.BytesRecorded} new {len}");
                        break;
                    case CompressionType.Mp3:
                    case CompressionType.Ogg:
                    default:
                        Console.WriteLine("not implemented yet");
                        throw new NotImplementedException();
                }
                try
                {
                    rwl.EnterReadLock();
                    //Console.Write("!");
                    foreach (var (nc, t) in connectedClients.Values)
                    {
                        if (nc.Muted) continue;
                        try
                        {
                            nc.Stream.BeginWrite(BitConverter.GetBytes(len), 0, sizeof(int), null, null);
                            nc.Stream.BeginWrite(buf, 0, len, null, null);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex);
                        }
                        //var w = nc.Writer;
                        //w.Write(len);
                        //w.Write(buf, 0, len);
                    }
                }
                finally
                {
                    rwl.ExitReadLock();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        readonly ReaderWriterLockSlim rwl = new ReaderWriterLockSlim();
        readonly Dictionary<uint, (NetworkIO nc, Thread t)> connectedClients = new Dictionary<uint, (NetworkIO, Thread)>();
        void ServerLoop()
        {
            tcpListener = new TcpListener(IPAddress.Any, Port);
            tcpListener.Start();
            while (true)
            {
                NetworkIO nc = null;
                try
                {
                    nc = new NetworkIO(tcpListener.AcceptTcpClient());
                    Console.WriteLine("Client trying to connect");
                    Thread t = null;
                    t = new Thread(() => ClientLoop(nc, t));
                    t.Start();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    nc?.Dispose();
                    continue;
                }
            }
        }
        void ClientLoop(NetworkIO nc, Thread t)
        {
            try
            {
                //Console.WriteLine("sending header");
                nc.SendHeader(SampleRate, Channels, Compression);
                //Console.WriteLine("receiving version");
                var ver = nc.ReceiveVersion();
                //Console.WriteLine($"version is {ver}, sending result");
                if (ver != Protocol.VERSION)
                {
                    Console.WriteLine("A client requested incompatible version " + ver);
                    nc.SendResult(1);
                    nc.Dispose();
                    return;
                }
                else nc.SendResult(0);

                nc.Muted = false;
                try
                {
                    rwl.EnterWriteLock();
                    connectedClients.Add(nc.Id, (nc, t));
                }
                finally
                {
                    rwl.ExitWriteLock();
                }
                Console.WriteLine($"({connectedClients.Count}) A client has connected");

                while (nc.Client.Connected)
                {
                    nc.Client.ReceiveTimeout = 10000;
                    //Console.WriteLine("reading ushort");
                    var p = nc.ReceiveMessageType();
                    switch (p)
                    {
                        case Protocol.MessageType.Ping:
                            //Console.WriteLine("got ping");
                            break;
                        case Protocol.MessageType.Mute:
                            nc.Muted = true;
                            break;
                        case Protocol.MessageType.Unmute:
                            nc.Muted = false;
                            break;
                        default: nc.Dispose(); return;
                    }
                }
            }
            catch (Exception ex)//yeah catching exception bad blah blah
            {
                Console.WriteLine(ex.Message);
                return;
            }
            finally
            {
                try
                {
                    rwl.EnterWriteLock();
                    connectedClients.Remove(nc.Id);
                }
                finally
                {
                    rwl.ExitWriteLock();
                    nc.Dispose();
                }
                Console.WriteLine($"({connectedClients.Count}) A client has disconnected");
            }
        }
    }
}
