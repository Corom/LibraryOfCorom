using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using Microsoft.Azure.WebJobs.Extensions;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Microsoft.Cognitive.Capabilities;

namespace DataEnricher
{
    public static class Program
    {
        static ConsoleLogger log = new ConsoleLogger(TraceLevel.Info);

        static void Main(string[] args)
        {
            foreach (var filepath in Directory.GetFiles(args[0]))
            {
                using (var file = File.OpenRead(filepath))
                    EnrichFunction.Run(file, Path.GetFileName(filepath), log).Wait();
            }
        }
        

        public class ConsoleLogger : TraceMonitor
        {
            public ConsoleLogger(TraceLevel level) : base(level)
            {
            }
            public override void Trace(TraceEvent traceEvent)
            {
                Console.WriteLine(traceEvent);
            }
        }

    }
}
