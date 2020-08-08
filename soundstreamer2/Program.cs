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

namespace soundstreamer2
{
    class Program
    {
        private static bool ConsoleWillBeDestroyedAtTheEnd()
        {
            var processList = new uint[1];
            var processCount = GetConsoleProcessList(processList, 1);

            return processCount == 1;
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        static extern uint GetConsoleProcessList(uint[] processList, uint processCount);

        public static readonly Dictionary<(string machinename, int id), ushort> VmPorts = new Dictionary<(string machinename, int id), ushort>()
        {
            { ("VM-1"           , 1), 7004 },//vm1
            { ("VM-2"           , 2), 7005 },//vm2
            { ("VM-3"           , 3), 7006 },//vm3
            { ("VM-4"           , 4), 7007 },//vm4
            { ("DESKTOP-RKFUK7A", 5), 7008 },//vm5
            { ("UNKNOWN-14C9F63", 6), 7009 },//vm6
        };
        static void Main(string[] args)
        {
            Environment.CurrentDirectory = new FileInfo(Path.GetFullPath(System.Reflection.Assembly.GetExecutingAssembly().Location)).Directory.FullName;

#if !DEBUG
            if (ConsoleWillBeDestroyedAtTheEnd())
            {
                Process.Start(new ProcessStartInfo("cmd.exe", "/k \"" + System.Reflection.Assembly.GetExecutingAssembly().Location + "\"") { UseShellExecute = false });
                return;
            }
#endif
#if DEBUG
            Console.Write("Cmd line: ");
            args = Console.ReadLine().Split(' ');
#endif

            Client client = null;
            Server server = null;
            bool ctorServer() { if (client != null) return false; if (server == null) server = new Server(); return true; }
            bool ctorClient() { if (server != null) return false; if (client == null) client = new Client(); return true; }
            bool skipFirstArg = true;
            if (args.Length > 0)
            {
                switch (args[0])
                {
                    case "client":
                    case "!server":
                        ctorClient();
                        break;
                    case "server":
                    case "!client":
                        ctorServer();
                        break;
                    default:
                        skipFirstArg = false;
                        break;
                }
            }
            if (server == null && client == null && VmPorts.Keys.Select(x => x.machinename).Contains(Environment.MachineName)) server = new Server();
            if (server == null && client == null) client = new Client();
            foreach (var arg in skipFirstArg ? args.Skip(1) : args)
            {
                if (string.IsNullOrWhiteSpace(arg)) continue;
                var arglower = arg.ToLowerInvariant();

                //yandev moment
                if (arglower.StartsWith("compress=") || arglower.StartsWith("ct=") || arglower.StartsWith("cmp="))
                {
                    if (!ctorServer()) break;
                    var s = arg.Substring(arg.IndexOf('=') + 1);
                    if (byte.TryParse(s, out var result))
                    {
                        server.Compression = result;
                    }
                    else if (Enum.TryParse(s, true, out CompressionType ct))
                    {
                        server.Compression = (byte)ct;
                    }
                    else Console.Error.WriteLine("invalid argument: " + arg);
                    if (server.Compression >= (byte)CompressionType.Mp3) throw new NotImplementedException((CompressionType)server.Compression + " compression not yet implemented");
                    continue;
                }
                else if (arglower.StartsWith("channels=") || arglower.StartsWith("ch="))
                {
                    if (!ctorServer()) break;
                    if (byte.TryParse(arg.Substring(arg.IndexOf('=') + 1), out var result))
                    {
                        server.Channels = result;
                    }
                    else Console.Error.WriteLine("invalid argument: " + arg);
                    continue;
                }
                else if (arglower.StartsWith("bufferms=") || arglower.StartsWith("ms="))
                {
                    if (!ctorServer()) break;
                    if (byte.TryParse(arg.Substring(arg.IndexOf('=') + 1), out var result))
                    {
                        server.BufferMs = result;
                    }
                    else Console.Error.WriteLine("invalid argument: " + arg);
                    continue;
                }

                else if (arglower.StartsWith("samplerate=") || arglower.StartsWith("sr="))
                {
                    if (!ctorServer()) break;
                    if (int.TryParse(arg.Substring(arg.IndexOf('=') + 1), out var result))
                    {
                        server.SampleRate = result;
                    }
                    else Console.Error.WriteLine("invalid argument: " + arg);
                    continue;
                }
                else if (arglower.StartsWith("token="))
                {
                    if (!ctorServer()) break;
                    server.UseNgrok = true;
                    server.NgrokToken = arg.Substring("token=".Length);
                    continue;
                }
                else if (arglower.StartsWith("region="))
                {
                    if (!ctorServer()) break;
                    server.UseNgrok = true;
                    var region = arg.Substring("region=".Length).ToLowerInvariant();
                    switch (region)
                    {
                        case "eu":
                        case "us":
                        case "au":
                        case "ap":
                            server.NgrokRegion = region;
                            break;
                        default:
                            Console.Error.WriteLine("unknown region: " + region);
                            Environment.Exit(-1);
                            return;
                    }
                    continue;
                }
                else if (arglower.StartsWith("port="))
                {
                    if (ushort.TryParse(arg.Substring("port=".Length), out var port))
                    {
                        if (client != null)
                        {
                            client.Port = port;
                            if (client.Hostname != null && client.Port != 0) client.Code = "";
                        }
                        else if (server != null) server.Port = port;
                        else Console.Error.WriteLine("need to specify if client or server.");
                    }
                    else Console.Error.WriteLine("invalid argument: " + arg);
                    continue;
                }
                else if (arglower.StartsWith("hostname=") || arglower.StartsWith("hn=") || arglower.StartsWith("ip="))
                {
                    if (!ctorClient()) break;
                    client.Hostname = arg.Substring(arg.IndexOf('=') + 1);
                    if (client.Hostname != null && client.Port != 0) client.Code = "";
                    continue;
                }
                else if (arglower.StartsWith("code=") || arglower.StartsWith("c="))
                {
                    if (!ctorClient()) break;
                    client.Code = arg.Substring(arg.IndexOf('=') + 1);
                    continue;
                }
                else if (arglower.StartsWith("id="))
                {
                    if (!ctorClient()) break;
                    if (int.TryParse(arg.Substring("id=".Length), out var id))
                    {
                        client.Id = id;
                        client.Code = "";
                    }
                    else Console.Error.WriteLine("invalid argument: " + arg);
                    continue;
                }
                switch (arglower)
                {
                    case "/?":
                    case "-?":
                    case "?":
                    case "/help":
                    case "-help":
                    case "-h":
                    case "/h":
                    case "help":
                        Console.WriteLine("just look at ze source lmao");
                        Environment.Exit(0);
                        break;
                    case "ngrok":
                    case "!d":
                        if (!ctorServer()) break;
                        server.UseNgrok = true;
                        break;
                    case "!ngrok":
                    case "d":
                        if (!ctorServer()) break;
                        server.UseNgrok = false;
                        break;
                    case "vac":
                        if (!ctorServer()) break;
                        server.UseVac = true;
                        break;
                    case "!vac":
                        if (!ctorServer()) break;
                        server.UseVac = false;
                        break;
                    case "eu":
                    case "e":
                        if (!ctorServer()) break;
                        server.UseNgrok = true;
                        server.NgrokRegion = "eu";
                        break;
                    case "us":
                    case "u":
                        if (!ctorServer()) break;
                        server.UseNgrok = true;
                        server.NgrokRegion = "us";
                        break;
                    case "au":
                    case "a":
                        if (!ctorServer()) break;
                        server.UseNgrok = true;
                        server.NgrokRegion = "au";
                        break;
                    case "ap":
                    case "p":
                        if (!ctorServer()) break;
                        server.UseNgrok = true;
                        server.NgrokRegion = "ap";
                        break;
                    default:
                        Console.Error.WriteLine("invalid command line argument: " + arg);
                        break;
                }
            }

            try
            {
                if (server != null) server.Main();
                else client.Main();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw;
            }

#if DEBUG
            Console.WriteLine("program execution finished");
            Console.ReadLine();
#endif
        }
    }
}
