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
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.Azure.EventGrid.Models;

public static async Task Run(EventGridEvent eventGridEvent, ILogger log){

    // Parse event data
    string eventData = eventGridEvent.Data.ToString();
    string subscriptionKey = "<Your Azure Maps subscription key>";
    var client = new HttpClient();
    
    await GetGeoAsync(eventData, client, subscriptionKey);
}

public static async Task GetGeoAsync(string eventData, HttpClient client, string subscriptionKey){

    dynamic jsonData = JsonConvert.DeserializeObject(eventData);
    
    string[] location = jsonData?.body["location"]["coordinates"].ToObject<string[]>();
    string time = jsonData?.systemProperties["iothub-enqueuedtime"];
    string deviceId = jsonData?.systemProperties["iothub-connection-device-id"];

    // Replace with the udId obtained after uploading geofence 
    string udid = "<udId>";
    
    // Request Azure Maps geofence API and parse response 
    string gUri = "https://atlas.microsoft.com/spatial/geofence/json?subscription-key={0}&api-version=1.0&deviceId={1}&isAsync=true&udId={2}&lat={3}&lon={4}&searchBuffer=2&mode=EnterAndExit";
    string geoUri = string.Format(gUri,subscriptionKey,deviceId,udid,location[1],location[0]);
    
    HttpResponseMessage response = await client.GetAsync(geoUri);
    string responseBody = await response.Content.ReadAsStringAsync();

    dynamic data = JsonConvert.DeserializeObject(responseBody);
    bool isEventPublished = data?.isEventPublished;
    int distance = data?.geometries[0]?.distance;
    
    string eventType;

    if (isEventPublished){
        
        if (distance<0){
            eventType = "Enter";
        }else{
            eventType = "Exit";
        }

        string query = location[1]+","+location[0];
        var addressCall = GetAddrAsync(client, subscriptionKey, query);
        var address = await addressCall;

        string violationCoords = location[0]+location[1];

        IDictionary<string, string> violation = new Dictionary<string, string>()
        {   
            {"Device Id", deviceId},
            {"Violation point",address},
            {"Location",violationCoords},
            {"Time",time},
            {"Event type",eventType}
        };

        string violationData = JsonConvert.SerializeObject(violation);
        string name = Guid.NewGuid().ToString("n")+".json";
        await CreateBlobAsync(name, violationData);
    }
}

// Calls Azure Maps reverse geocode API to get address
public static async Task<string> GetAddrAsync(HttpClient client, string subscriptionKey, string query){

    string aUri = "https://atlas.microsoft.com/search/address/reverse/json?subscription-key={0}&api-version=1.0&query={1}";
    string url = string.Format(aUri,subscriptionKey,query);

    HttpResponseMessage AdrsResponse = await client.GetAsync(url);
    
    string AdrsResponseBody = await AdrsResponse.Content.ReadAsStringAsync();
    dynamic adrsData = JsonConvert.DeserializeObject(AdrsResponseBody);
    string address = adrsData?.addresses?[0]?.address?.freeformAddress;

    return address;
}

// Creates and writes to a blob in data storage
public static async Task CreateBlobAsync(string name, string violationData){

    //Replace accessKey, accountName with your storage account access key and account name
	string accessKey = "<Access key>";
    string accountName = "<Account name>";
    string connectionString = "DefaultEndpointsProtocol=https;AccountName=" + accountName + ";AccountKey=" + accessKey + ";EndpointSuffix=core.windows.net";;
    CloudStorageAccount storageAccount;

	storageAccount = CloudStorageAccount.Parse(connectionString);

    CloudBlobClient BlobClient;
    CloudBlobContainer container;
    BlobClient = storageAccount.CreateCloudBlobClient();
    container = BlobClient.GetContainerReference("rentaldata");

    await container.CreateIfNotExistsAsync();
    CloudBlockBlob blob;
    blob = container.GetBlockBlobReference(name);
    
    await blob.UploadTextAsync(violationData);

    blob.Properties.ContentType = "application/json";
}