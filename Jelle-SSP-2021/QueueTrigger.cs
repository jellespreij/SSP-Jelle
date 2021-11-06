using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Jelle_SSP_2021
{
    public static class QueueTrigger
    {
        [FunctionName("QueueFetchImages")]
        public static async Task QueueFetchImages([QueueTrigger("images")] string myQueueItem)
        {
            List<string> weatherText = await GetWeatherText();

            List<Bitmap> imageList1 = await GetImages(25);
            List<Bitmap> imageList2 = await GetImages(26);

            List<Bitmap> completeImageList = imageList1.Concat(imageList2).ToList();

            for (int i = 0; i < completeImageList.Count; i++)
            {
                Bitmap image = AddTextToImage(completeImageList[i], weatherText[i]);
                await SendAsBlob(image, i, myQueueItem);
            }
        }

        private static async Task<List<string>> GetWeatherText()
        {
            string url = "https://data.buienradar.nl/2.0/feed/json";

            HttpClient newClient = new HttpClient();
            HttpRequestMessage newRequest = new HttpRequestMessage(HttpMethod.Get, string.Format(url));

            HttpResponseMessage response = await newClient.SendAsync(newRequest);
            var json = await response.Content.ReadAsStringAsync();

            dynamic data = JObject.Parse(json);

            List<string> weatherText = new List<string>();

            foreach (var weatherStation in data.actual.stationmeasurements)
            {
                string weatherStationName = weatherStation.stationname.ToString();
                string weatherDescription = weatherStation.weatherdescription.ToString();

                weatherText.Add(weatherStationName + ": " + weatherDescription);
            }

            return weatherText;
        }

        public static async Task<List<Bitmap>> GetImages(int count)
        {
            string unsplashUrl = "https://api.unsplash.com/photos/random/";
            string unsplashParam = ($"?client_id=sqV1UlwVGBaoJtJ5s8vayvdctwsErRSayzrdvVhkjeU&count={count}");
            string requestUrl = unsplashUrl + unsplashParam;

            HttpClient newClient = new HttpClient();
            HttpRequestMessage newRequest = new HttpRequestMessage(HttpMethod.Get, string.Format(requestUrl));
            HttpResponseMessage response = await newClient.SendAsync(newRequest);
            List<Bitmap> bitmap = new List<Bitmap>();
            var json = await response.Content.ReadAsStringAsync();

            JArray tmp = JsonConvert.DeserializeObject<JArray>(json);

            foreach (JObject img in tmp)
            {
                dynamic data = img;
                if (data.links != null && data.errors == null)
                {
                    string url = data.urls.regular;
                    string urlString = url.ToString();
                    var stream = await newClient.GetStreamAsync(urlString);
                    Bitmap bit = new Bitmap(stream);
                    bitmap.Add(bit);
                }
            }
            return bitmap;
        }

        private static Bitmap AddTextToImage(Bitmap image, string weatherText)
        {
            Font font = new Font("Arial", 50);
            Point point = new Point(10, 10);

            Graphics graphics = Graphics.FromImage(image);
            using (font)
            {
                graphics.DrawString(weatherText, font, Brushes.Blue, point);
            }

            graphics.Dispose();
            MemoryStream stream = new MemoryStream();
            image.Save(stream, System.Drawing.Imaging.ImageFormat.Jpeg);
            return image;
        }

        private static async Task SendAsBlob(Bitmap image, int imageNumber, string id)
        {
            using (var memoryStream = new MemoryStream())
            {
                string storageConnectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
                CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storageConnectionString);
                image.Save(memoryStream, ImageFormat.Jpeg);
                memoryStream.Position = 0;

                var blobClient = storageAccount.CreateCloudBlobClient();

                var cloudBlobContainer = blobClient.GetContainerReference(id);
                await cloudBlobContainer.CreateIfNotExistsAsync();

                string fileName = imageNumber.ToString() + ".jpeg";
                var cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference(fileName);

                await cloudBlockBlob.UploadFromStreamAsync(memoryStream);
            }
        }
    }
}