using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Cognitive.Capabilities
{
    public class ImageStore
    {
        private CloudBlobContainer libraryContainer;

        public ImageStore(string blobConnectionString, string containerName)
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(blobConnectionString);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            libraryContainer = blobClient.GetContainerReference(containerName);
        }

        public async Task<string> UploadImageToLibrary(Stream stream, string name)
        {
            CloudBlockBlob blockBlob = libraryContainer.GetBlockBlobReference(name);
            await blockBlob.UploadFromStreamAsync(stream);

            return blockBlob.Uri.ToString();
        }

        public Task<string> UploadToBlob(byte[] data, string name)
        {
            return UploadImageToLibrary(new MemoryStream(data), name);
        }

    }
}
