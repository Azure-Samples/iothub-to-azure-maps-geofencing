import sys
import time
import json
import iothub_client
from iothub_client import IoTHubClient, IoTHubClientError, IoTHubTransportProvider, IoTHubClientResult
from iothub_client import IoTHubMessage, IoTHubMessageDispositionResult, IoTHubError, DeviceMethodReturnValue

# The device connection string to authenticate the device with IoT hub
CONNECTION_STRING = "<Connection string>"

# Using the MQTT protocol
PROTOCOL = IoTHubTransportProvider.MQTT
MESSAGE_TIMEOUT = 10000

# Read route telemetry from Json file
with open('route.json') as f:
    data = json.load(f)

positions = data['features'][0]['geometry']['coordinates']

MSG = {
        "location": { 
            "type": "Point", 
            "coordinates": ["lat", "lon"]
        } 
      }

def send_confirmation_callback(message, result, user_context):
    print ( "IoT Hub responded to message with status: %s" % (result) )

def iothub_client_init():
    # Create an IoT Hub client
    client = IoTHubClient(CONNECTION_STRING, PROTOCOL)
    client.set_option("auto_url_encode_decode", True)
    return client

# Send device telemetry to IoT hub
def iothub_client_telemetry():

    try:
        client = iothub_client_init()
        print ( "in-vehicle device sending periodic messages, press Ctrl-C to exit" )
        time_interval = 0

        for loc in range(len(positions)):
                location = positions[loc]

                for i in range(2):
                    MSG['location']['coordinates'][i] = location[i]
                
                encoded_Message = json.dumps(MSG).encode('utf8')
                message = IoTHubMessage(encoded_Message)
                
                # Encode and set content type before sending to IoT hub
                message.set_content_encoding_system_property("utf-8")
                message.set_content_type_system_property("application/json")
                 
                # Add Eangine status as an application property to the message
                prop_map = message.properties()
                if loc>0 and location==positions[loc-1]:
                    time_interval+=1
                    if time_interval==5:
                        prop_map.add("Engine", "OFF")
                else:
                    time_interval=0
                    prop_map.add("Engine", "ON")
                
                # Send the message.
                print( "Sending message: %s" % message.get_string() )
                client.send_event_async(message, send_confirmation_callback, None)
                time.sleep(1)

    except IoTHubError as iothub_error:
        print ( "Unexpected error %s from IoTHub" % iothub_error )
        return
    except KeyboardInterrupt:
        print ( "IoTHubClient stopped" )

if __name__ == '__main__':
    print ( "Press Ctrl-C to exit" )
    iothub_client_telemetry()
