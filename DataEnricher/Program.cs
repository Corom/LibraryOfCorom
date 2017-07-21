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

                    Console.WriteLine("Services have been successfully Initialized");
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
            catch (Exception e)
            {
                Console.WriteLine("An Error has occured: " + e.ToString());
            }

            Console.WriteLine();
            Console.WriteLine("Done.");
            Console.WriteLine("Press enter to exit");
            Console.ReadLine();
        }


        static void InitializeServices()
        {
            // create the storage containers if needed
            CloudBlobClient blobClient = CloudStorageAccount.Parse($"DefaultEndpointsProtocol=https;AccountName={EnrichFunction.IMAGE_AZURE_STORAGE_ACCOUNT_NAME};AccountKey={EnrichFunction.IMAGE_BLOB_STORAGE_ACCOUNT_KEY};EndpointSuffix=core.windows.net").CreateCloudBlobClient();
            blobClient.GetContainerReference(EnrichFunction.IMAGE_BLOB_STORAGE_CONTAINER).CreateIfNotExists(BlobContainerPublicAccessType.Blob);
            blobClient.GetContainerReference(EnrichFunction.LIBRARY_BLOB_STORAGE_CONTAINER).CreateIfNotExists(BlobContainerPublicAccessType.Off);

            // create the index if needed
            var serviceClient = new SearchServiceClient(EnrichFunction.AZURE_SEARCH_SERVICE_NAME, new SearchCredentials(EnrichFunction.AZURE_SEARCH_ADMIN_KEY));
            if (!serviceClient.Indexes.List().Indexes.Any(i => i.Name == EnrichFunction.AZURE_SEARCH_INDEX_NAME))
            {
                var definition = new Index()
                {
                    Name = EnrichFunction.AZURE_SEARCH_INDEX_NAME,
                    Fields = FieldBuilder.BuildForType<HOCRDocument>(),
                    CorsOptions = new CorsOptions()
                    {
                        AllowedOrigins = new[] { "*" }
                    }
                };

                serviceClient.Indexes.CreateOrUpdate(definition);
            }

            // test the pipeline and index
            Console.WriteLine("Sending a test image through the pipeline");
            using (var file = File.OpenRead(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-image.jpg")))
            {
                EnrichFunction.Run(file, "TEST_IMAGE", log).Wait();
            }

            Console.WriteLine("Querying the test image");
            var indexClient = serviceClient.Indexes.GetClient(EnrichFunction.AZURE_SEARCH_INDEX_NAME);
            var results = indexClient.Documents.Search("ABC12345XYZ", new SearchParameters()
            {
                Facets = new[] { "tags", "people", "places", "adult", "racy" },
                HighlightFields = new[] { "text" },
            });

            // TODO: Add some additional validations for fields
            if (results.Results.Count > 0)
                Console.WriteLine("Item found in index");
            else
                Console.WriteLine("Item missing from index");

            Console.WriteLine("Delete the test item");
            var deleteResult = indexClient.Documents.Index(IndexBatch.Delete("id", new[] { "TEST_IMAGE" }));

            if (deleteResult.Results.Count > 0)
                Console.WriteLine("Item deleted from the index");
            else
                Console.WriteLine("could not delete the item");
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
