using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Jelle_SSP_2021
{
    public class HttpTriggers
    {
        [FunctionName("CreateWeatherStationImages")]
        public async Task<IActionResult> CreateWeatherStationImages(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "create-images")] HttpRequest req,
            [Queue("images")] IAsyncCollector<string> applicationQueue,
            ILogger log)
        {
            Random randInt = new Random();
            int id = randInt.Next(0, 9999);

            applicationQueue.AddAsync(id.ToString());

            return (IActionResult)new OkObjectResult("start creating images, use this id to retrieve your created images: " + id.ToString());
        }

        [FunctionName("GetWeatherStationImages")]
        public async Task<IActionResult> GetWeatherStationImages(
           [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "get-images/{id:int}")] HttpRequest req,
           string id,
           ILogger log)
        {
            List<String> imagesUrls = new List<String>();
            string storageConnectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storageConnectionString);

            var blobClient = storageAccount.CreateCloudBlobClient();

            var cloudBlobContainer = blobClient.GetContainerReference(id);
            await cloudBlobContainer.CreateIfNotExistsAsync();

            BlobResultSegment resultSegment = await cloudBlobContainer.ListBlobsSegmentedAsync(string.Empty,
                true, BlobListingDetails.Metadata, 100, null, null, null);

            if (resultSegment.Results.Count() != 51)
            {
                return (IActionResult)new OkObjectResult("Images are still being created " + resultSegment.Results.Count().ToString() + "/51 images done");
            }

            Console.WriteLine(resultSegment.Results.Count());

            foreach (var blobItem in resultSegment.Results)
            {
                var blob = (CloudBlob)blobItem;
                var cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference(blob.Name);
                if (cloudBlockBlob.Uri.ToString() != null)
                {
                    imagesUrls.Add(cloudBlockBlob.Uri.ToString());
                }
            }

            return (IActionResult)new OkObjectResult(imagesUrls);
        }
    }
}
