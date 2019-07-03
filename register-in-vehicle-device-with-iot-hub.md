### Register a device

In order to connect to the IoT hub and send data to it, a device must be registered. This guide teaches you how to register a device with IoT Hub.

1. [Open up the Azure Cloud Shell (CLI)](https://docs.microsoft.com/en-us/azure/iot-hub/quickstart-send-telemetry-python#open-azure-cloud-shell) and run the following command to create the device identity.

    ```Azure CLI
    az iot hub device-identity create --hub-name {YourIoTHubName} --device-id {MyPythonDevice}
    ```
   Replace **YourIoTHubName** and **MyPythonDevice** with your IoT hub and device names.

2. Run the following command in Azure Cloud Shell to get the **device connection string** for the device you registered.

    ```Azure CLI
    az iot hub device-identity show-connection-string --hub-name {YourIoTHubName} --device-id MyPythonDevice --output table
    ```
    Replace **YourIoTHubName** with your IoT hub name.

3. Save the **device connection string**, which will be similar to the one below, we will use it later:

    ```
    HostName={YourIoTHubName}.azure-devices.net;DeviceId=InVehicleDevice;SharedAccessKey={YourSharedAccessKey}
    ```
