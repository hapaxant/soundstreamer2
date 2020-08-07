using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DownloadFile
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2) throw new ArgumentException();
            //try
            //{
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var req = WebRequest.CreateHttp(args[0]);
            using (var wr = req.GetResponse())
            using (var rs = wr.GetResponseStream())
            using (var fs = File.Create(args[1]))
                rs.CopyTo(fs);
            //}
            //catch
            //{
            //    Console.WriteLine("please download file manually.");
            //    System.Diagnostics.Process.Start(args[0]);
            //    Console.WriteLine("press enter when done.");
            //    Console.ReadLine();
            //}
        }
    }
}
