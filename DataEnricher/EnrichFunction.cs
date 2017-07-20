// Azure Function references
#if !LOCAL_EXECUTION
#r "System.IO"
#r "System.Drawing"
#r "Microsoft.Cognitive.Capabilities.dll"
#endif

// implicit using statements when used as an Azure functions
#if LOCAL_EXECUTION
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
#endif


using Newtonsoft.Json;
using System.Net;
using Microsoft.Cognitive.Capabilities;
using System.Drawing;
using System;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;


#if LOCAL_EXECUTION
namespace DataEnricher
{
    public static class EnrichFunction
    {
        static EnrichFunction()
        {
            Init();
        }

#endif
        /**************  UPDATE THESE CONSTANTS WITH YOUR SETTINGS  **************/

        // Use handwriting API for OCR otherwise uses standard OCR which works better with text
        internal const bool USE_HANDWRITING_OCR = true;

        // Azure Blob Storage used to store extracted page images
        internal const string IMAGE_AZURE_STORAGE_ACCOUNT_NAME = "";
        internal const string IMAGE_BLOB_STORAGE_ACCOUNT_KEY = "";
        internal const string IMAGE_BLOB_STORAGE_CONTAINER = "";

        // Cognitive Services Vision API used to process images
        internal const string VISION_API_KEY = "";

        // AzureML Webservice used for Entity Extraction
        internal const string AZURE_ML_WEBSERVICE_URL = "";
        internal const string AZURE_ML_WEBSERVICE_API_KEY = "";

        // Azure Search service used to index documents
        internal const string AZURE_SEARCH_SERVICE_NAME = "";
        internal const string AZURE_SEARCH_ADMIN_KEY = "";
        internal const string AZURE_SEARCH_INDEX_NAME = "";

        /*************************************************************************/

        static ImageStore blobContainer;
        static EntityExtractor entityExtractor;
        static Vision visionClient;
        static HttpClient httpClient = new HttpClient();
        static ISearchIndexClient indexClient;

        static void Init()
        {
            if (blobContainer == null)
            {
                // init the blob client
                blobContainer = new ImageStore($"DefaultEndpointsProtocol=https;AccountName={IMAGE_AZURE_STORAGE_ACCOUNT_NAME};AccountKey={IMAGE_BLOB_STORAGE_ACCOUNT_KEY};EndpointSuffix=core.windows.net", IMAGE_BLOB_STORAGE_CONTAINER);
                entityExtractor = new EntityExtractor(AZURE_ML_WEBSERVICE_URL, AZURE_ML_WEBSERVICE_API_KEY);
                visionClient = new Vision(VISION_API_KEY);
                var serviceClient = new SearchServiceClient(AZURE_SEARCH_SERVICE_NAME, new SearchCredentials(AZURE_SEARCH_ADMIN_KEY));
                indexClient = serviceClient.Indexes.GetClient(AZURE_SEARCH_INDEX_NAME);
            }
        }


        public static async Task Run(Stream blobStream, string name, TraceWriter log)
        {
            Init();
            log.Info($"Processing blob:{name}");

            // Extract each scanned page from the blob
            var searchDocument = new HOCRDocument(name);
            foreach (var page in ImageHelper.ConvertToJpegs(blobStream, 2000, 2000))
            {
                // Send image to Vision API handwriting service
                var imageUrl = await blobContainer.UploadToBlob(page, $"{name}/{searchDocument.PageCount}");
                var hwResult = await (USE_HANDWRITING_OCR ?  visionClient.GetHandwritingText(imageUrl) : visionClient.GetText(imageUrl));
                var visionResult = await visionClient.GetVision(imageUrl);
                searchDocument.Racy = visionResult.RacyScore * 1000;  // make the score a bigger number since the AzSearch libary range facet only uses increments of 1
                searchDocument.Adult = visionResult.AdultScore * 1000;
                searchDocument.Tags = visionResult.Tags.ToList();
                searchDocument.AddPage(hwResult.Concat(visionResult), imageUrl);
            }

            // Extract Named entities and add them to the document
            var entities = await entityExtractor.Extract(searchDocument.Text);
            searchDocument.PeopleFacet = entities.Where(e => e.EntityType == EntityType.Person).Select(e => e.Name).Distinct().ToArray();
            searchDocument.PlacesFacet = entities.Where(e => e.EntityType == EntityType.Location).Select(e => e.Name).Distinct().ToArray();

            // push document to the azure search index
            var batch = IndexBatch.MergeOrUpload(new[] { searchDocument });
            var result = await indexClient.Documents.IndexAsync(batch);

            if (!result.Results[0].Succeeded)
                log.Error($"index failed for {name}: {result.Results[0].ErrorMessage}");
        }


#if LOCAL_EXECUTION
    }
}
#endif