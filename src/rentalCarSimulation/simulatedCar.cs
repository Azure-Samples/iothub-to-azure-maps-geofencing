using System;
using System.IO;
using System.Text;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.Devices.Client;


namespace deviceSimulation
{
    class SimulatedDevice
    {
        private static DeviceClient deviceClient;

        // The device connection string to authenticate the device with your IoT hub.
        private readonly static string connectionString = "<IoTHub_device_connection_string>";

        // Read route telemetry from Json file
        static dynamic routeData = JsonConvert.DeserializeObject(File.ReadAllText(@"data/route.json"));

        static JArray locations = routeData["features"][0]["geometry"]["coordinates"];

        static List<List<double>> coordList = locations.ToObject<List<List<double>>>();

        // Async method to send simulated telemetry
        private static async void SendDeviceToCloudMessagesAsync()
        {
            int timeInterval = 0;

            for (int loc = 0; loc < coordList.Count; loc++)
            {
                // Device message schema
                var LocMSG = new
                {
                    location = new
                    {
                        type = "Point",
                        coordinates = coordList[loc]
                    }
                };

                var MSG = JsonConvert.SerializeObject(LocMSG);
                var message = new Message(Encoding.ASCII.GetBytes(MSG));
                message.ContentEncoding = "utf-8";
                message.ContentType = "application/json";

                // If the location doesn't change for two minutes, switch the engine status to "OFF"
                if (loc > 0 && coordList[loc].SequenceEqual(coordList[loc-1]))
                {

                    timeInterval += 1;
                    if (timeInterval == 2)
                    {
                        message.Properties.Add("ENGINE", "OFF");
                    }
                }
                else
                {
                    timeInterval = 0;
                    message.Properties.Add("ENGINE", "ON");
                }

                // Send the telemetry message
                await deviceClient.SendEventAsync(message);
                Console.WriteLine("{0} > Sending message: {1}", DateTime.Now, MSG);
                await Task.Delay(1000);
            }
        }

        private static void Main(string[] args)
        {
            Console.WriteLine("in-vehicle device sending periodic messages, press Ctrl-C to exit.\n");

            // Connect to the IoT hub using the MQTT protocol
            deviceClient = DeviceClient.CreateFromConnectionString(connectionString, TransportType.Mqtt);
            SendDeviceToCloudMessagesAsync();
            Console.ReadLine();
        }
    }

}
