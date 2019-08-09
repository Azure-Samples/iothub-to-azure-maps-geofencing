#r "Microsoft.Azure.EventGrid"
#r "Microsoft.WindowsAzure.Storage"
#r "Newtonsoft.Json"

using System.Net;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.WindowsAzure.Storage.Blob;

const string SUBSCRIPTION_KEY = "<Your Azure Maps subscription key>";
const string UDID = "<UDID>";
const string STORAGE_ACCESS_KEY = "<Storage Access key>";
const string STORAGE_ACCOUNT_NAME = "<Storage Account name>";
const string STORAGE_CONTAINER_NAME = "Storage data container name";

public static async Task Run(EventGridEvent eventGridEvent, ILogger log){

    // Parse event data 
    string eventData = eventGridEvent.Data.ToString();
    var client = new HttpClient();
    
    await GetGeoAsync(eventData, client, SUBSCRIPTION_KEY, log);
}

public static async Task GetGeoAsync(string eventData, HttpClient client, string subscriptionKey, ILogger log){

    dynamic jsonData = JsonConvert.DeserializeObject(eventData);
    
    string[] location = jsonData?.body["location"]["coordinates"].ToObject<string[]>();
    string time = jsonData?.systemProperties["iothub-enqueuedtime"];
    string deviceId = jsonData?.systemProperties["iothub-connection-device-id"];

    // Request Azure Maps geofence API and parse response 
    string gUri = "https://atlas.microsoft.com/spatial/geofence/json?subscription-key={0}&api-version=1.0&deviceId={1}&isAsync=true&udId={2}&lat={3}&lon={4}&searchBuffer=2&mode=EnterAndExit";
    string geoUri = string.Format(gUri,subscriptionKey,deviceId,UDID,location[1],location[0]);
    
    HttpResponseMessage response = await client.GetAsync(geoUri);
    string responseBody = await response.Content.ReadAsStringAsync();

    dynamic data = JsonConvert.DeserializeObject(responseBody);
    int distance = data?.geometries[0]?.distance;
    
    log.LogInformation("Location: [{lat}, {long}], Distance: {distance}", location[1], location[0], distance);

    if (distance>0){

        string query = location[1]+","+location[0];
        var addressCall = GetAddrAsync(client, subscriptionKey, query);
        var address = await addressCall;

        string[] violationCoords = {location[0],location[1]};
        
        var violation = new
        {
            deviceId = deviceId,
            violationPoint = address,
            Location = violationCoords,
            Time = time
        };

        string violationData = JsonConvert.SerializeObject(violation);
        string name = Guid.NewGuid().ToString("n")+".json";
        await CreateBlobAsync(name, violationData);
    }
}

// Calls Azure Maps reverse geocode API to get address
public static async Task<string> GetAddrAsync(HttpClient client, string subscriptionKey, string query){

    string aUri = "https://atlas.microsoft.com/search/address/reverse/json?subscription-key={0}&api-version=1.0&query={1}";
    string url = string.Format(aUri,SUBSCRIPTION_KEY,query);

    HttpResponseMessage AdrsResponse = await client.GetAsync(url);
    
    string AdrsResponseBody = await AdrsResponse.Content.ReadAsStringAsync();
    dynamic adrsData = JsonConvert.DeserializeObject(AdrsResponseBody);
    string address = adrsData?.addresses?[0]?.address?.freeformAddress;

    return address;
}

// Creates and writes to a blob in data storage
public static async Task CreateBlobAsync(string name, string violationData){

	string connectionString = "DefaultEndpointsProtocol=https;AccountName=" + STORAGE_ACCOUNT_NAME + ";AccountKey=" +  STORAGE_ACCESS_KEY + ";EndpointSuffix=core.windows.net";
    CloudStorageAccount storageAccount;

	storageAccount = CloudStorageAccount.Parse(connectionString);

    CloudBlobClient BlobClient;
    CloudBlobContainer container;
    BlobClient = storageAccount.CreateCloudBlobClient();
    container = BlobClient.GetContainerReference(STORAGE_CONTAINER_NAME);

    await container.CreateIfNotExistsAsync();
    CloudBlockBlob blob;
    blob = container.GetBlockBlobReference(name);
    
    await blob.UploadTextAsync(violationData);

    blob.Properties.ContentType = "application/json";
}