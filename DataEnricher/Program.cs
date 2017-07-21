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
            try
            {
                if (args.Length == 0)
                {
                    Console.WriteLine("Initializing Services");
                    InitializeServices();
                }
                else
                {
                    Console.WriteLine("Indexing images under " + args[0]);
                    foreach (var filepath in Directory.GetFiles(args[0]))
                    {
                        using (var file = File.OpenRead(filepath))
                            EnrichFunction.Run(file, Path.GetFileName(filepath), log).Wait();
                    }
                }
            }
            catch(Exception e)
            {
                Console.WriteLine("An Error has occured: " + e.ToString());
            }
        }


        static void InitializeServices()
        {
            // create the storage containers if needed
            CloudBlobClient blobClient = CloudStorageAccount.Parse($"DefaultEndpointsProtocol=https;AccountName={EnrichFunction.IMAGE_AZURE_STORAGE_ACCOUNT_NAME};AccountKey={EnrichFunction.IMAGE_BLOB_STORAGE_ACCOUNT_KEY};EndpointSuffix=core.windows.net").CreateCloudBlobClient();
            blobClient.GetContainerReference(EnrichFunction.IMAGE_BLOB_STORAGE_CONTAINER).CreateIfNotExists(BlobContainerPublicAccessType.Blob);
            blobClient.GetContainerReference(EnrichFunction.LIBRARY_BLOB_STORAGE_CONTAINER).CreateIfNotExists(BlobContainerPublicAccessType.Off);


            var serviceClient = new SearchServiceClient(EnrichFunction.AZURE_SEARCH_SERVICE_NAME, new SearchCredentials(EnrichFunction.AZURE_SEARCH_ADMIN_KEY));
            if (!serviceClient.Indexes.List().Indexes.Any(i => i.Name == EnrichFunction.AZURE_SEARCH_INDEX_NAME))
            {
                var definition = new Index()
                {
                    Name = EnrichFunction.AZURE_SEARCH_INDEX_NAME,
                    Fields = FieldBuilder.BuildForType<HOCRDocument>(),
                    CorsOptions = new CorsOptions() {
                        AllowedOrigins = new[] { "*" }
                    }
                };

                serviceClient.Indexes.CreateOrUpdate(definition);
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
