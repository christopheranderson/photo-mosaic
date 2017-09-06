using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Web;
using Microsoft.WindowsAzure.Storage.Blob;

namespace MosaicMaker
{
    public static class DownloadImages
    {
        private const string SubscriptionKeyHeader = "Ocp-Apim-Subscription-Key";
        private const string SearchApiKeyName = "SearchAPIKey";
        private const string BingSearchUri = "https://api.cognitive.microsoft.com/bing/v5.0/images/search";

        public static async Task<List<string>> GetImageResultsAsync(string query, TraceWriter log)
        {
            var result = new List<string>();

            try {
                var httpClient = new HttpClient();

                var builder = new UriBuilder(BingSearchUri);
                var queryParams = HttpUtility.ParseQueryString(string.Empty);
                queryParams["q"] = query.ToString();
                queryParams["count"] = "100";

                builder.Query = queryParams.ToString();

                var request = new HttpRequestMessage() {
                    RequestUri = builder.Uri,
                    Method = HttpMethod.Get
                };

                var apiKey = Environment.GetEnvironmentVariable(SearchApiKeyName);
                request.Headers.Add(SubscriptionKeyHeader, apiKey);
                request.Headers.Add("User-Agent", "Mozilla/5.0 (compatible; MSIE 10.0; Windows Phone 8.0; Trident/6.0; IEMobile/10.0; ARM; Touch; NOKIA; Lumia 822)");

                var response = await httpClient.SendAsync(request);
                
                if (response.IsSuccessStatusCode) {
                    var resultString = await response.Content.ReadAsStringAsync();
                    var resultObject = JObject.Parse(resultString);

                    if (resultObject == null) {
                        log.Info("ERROR: No results from image search");
                    }

                    var images = resultObject["value"];

                    foreach (var imageInfo in images) {
                        result.Add(imageInfo["thumbnailUrl"].ToString());
                    }
                }
            }
            catch (Exception e) {
                log.Info($"Exception during image search: {e.Message}");
            }

            return result;
        }

        public static async Task DownloadImagesAsync(string queryId, List<string> imageUrls, CloudBlobContainer outputContainer)
        {
            var httpClient = new HttpClient();

            var tileWidth = Environment.GetEnvironmentVariable("MosaicTileWidth");
            var tileHeight = Environment.GetEnvironmentVariable("MosaicTileHeight");

            foreach (var url in imageUrls) {
                try {
                    var resizedUrl = $"{url}&w={tileWidth}&h={tileHeight}&c=7";
                    var responseStream = await httpClient.GetStreamAsync(resizedUrl);

                    var queryString = HttpUtility.ParseQueryString(new Uri(url).Query);
                    var imageId = queryString["id"] + ".jpg";

                    var dir = outputContainer.GetDirectoryReference(queryId);
                    var blob = dir.GetBlockBlobReference(imageId);
                    await blob.UploadFromStreamAsync(responseStream);
                }
                catch (Exception) {
                    continue;
                }
            }
        }
    }
}
