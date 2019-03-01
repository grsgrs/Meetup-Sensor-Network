using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UwpBluetooth
{
  public static class AzureIotCentralClient
  {
    #region class variables
    // delegate for show messages in UI
    public delegate void ShowMessageDelegate(string Message);
    public static ShowMessageDelegate ShowEventMessage;
    public static ShowMessageDelegate ShowTelemetryMessage;

    // delegate for beacon/sensor enable/disable
    public delegate Task SensorChangedDelegate (string bluetoothaddressAsString, string sensorAsString, string actionAsString);
    public static SensorChangedDelegate SensorChanged;

    // version for simulate config update
    private static String Version = "1.1.5";
    // IoT Hub Device client
    static DeviceClient _deviceClient;
    // device id
    public const string CentralDeviceId = "<CentralDeviceId>";
    // connectionStatus string
    public const string CentralConnectionString = "<CentralConnectionString>";
    #endregion

    #region connect to iot central
    public static void ConnectToIoTCentral()
    {
      // Azure IoT Hub connection
      _deviceClient =
        DeviceClient.CreateFromConnectionString(CentralConnectionString, TransportType.Mqtt);
      
      // TODO V3
      // sets delegates for iot commands
      Task.Run(async () => await _deviceClient.SetMethodHandlerAsync("config", OnConfigchanged, null).ConfigureAwait(false));
      Task.Run(async () => await _deviceClient.SetMethodHandlerAsync("sensor", OnSensorChanged, null).ConfigureAwait(false));
      Task.Run(async () => await _deviceClient.SetMethodHandlerAsync("deviceupdate", OnDeviceChanged, null).ConfigureAwait(false));
      
    }
    #endregion

    #region send sensor data
    // send senor data to iot central, for all sensors of beacon
    public static async Task SendSensorDataToIotCentral(Beacon beacon, DateTime timeStampReceived)
    {
      // TODO V1
      // create new telemetry message
      JTokenWriter writer = new JTokenWriter();
      writer.WriteStartObject();

      // Timestamp values received
      writer.WritePropertyName("timestamp");
      writer.WriteValue(timeStampReceived.ToUniversalTime());

      // Azuire Iot Central device id
      writer.WritePropertyName("deviceid");
      writer.WriteValue(CentralDeviceId);

      // Iterate sensor data
      foreach (var sensorData in beacon.SensorDatas)
      {
        // Only active sensors
        if (sensorData.Enabled == false)
        {
          continue;
        }

        // Key => value
        writer.WritePropertyName($"Bt{beacon.BluetoothAddress:X}_{(int)sensorData.SensorType}");
        writer.WriteValue(sensorData.Value);
        writer.WritePropertyName($"Bt{beacon.BluetoothAddress:X}_{(int)sensorData.SensorType}_meta");
        writer.WriteValue(beacon.Description);
      }
      writer.WriteEndObject();

      JObject o = (JObject)writer.Token;

      // serialise message to a JSON string
      string messageString = o.ToString();
      Debug.WriteLine(messageString);

      // format JSON string into IoT Hub message
      Message message = new Message(Encoding.ASCII.GetBytes(messageString));

      // push message to IoT Hub
      try
      {
        await _deviceClient.SendEventAsync(message).ConfigureAwait(false);
        ShowTelemetryMessage?.Invoke($"Success SendSensorDataToIotCentral BLE {beacon.Name}");
      }
      catch (Exception e)
      {
        ShowEventMessage?.Invoke($"Exception SendSensorDataToIotCentral {e.Message}");
      }
    }
    #endregion

    #region send message to iot central
    // send a message to iot central, topic is the key
    public static async Task SendMessageFromDeviceToCloud(string topic, string messageText)
    {
      JTokenWriter writer = new JTokenWriter();
      writer.WriteStartObject();
      writer.WritePropertyName("time");
      writer.WriteValue(DateTime.Now.ToString(CultureInfo.InvariantCulture));
      writer.WritePropertyName("deviceId");
      writer.WriteValue(CentralDeviceId);
      writer.WritePropertyName(topic);
      writer.WriteValue(messageText);
      writer.WriteEndObject();

      JObject o = (JObject)writer.Token;

      // serialise message to a JSON string
      string messageString = o.ToString();

      // format JSON string into IoT Hub message
      Message message = new Message(Encoding.ASCII.GetBytes(messageString));

      // push message to IoT Hub
      try
      {
        await _deviceClient.SendEventAsync(message).ConfigureAwait(false);
        ShowEventMessage?.Invoke($"Success Send to azure topic {topic} message {messageText}");
      }
      catch (Exception e)
      {
        ShowEventMessage?.Invoke($"Error Send to azure topic {topic} message {messageText}");
        ShowEventMessage?.Invoke(e.Message);
      }

    }
    #endregion

    #region receive messages from iot central
    // processes deviceupdate command from iot central => disable/enable device
    private static async Task<MethodResponse> OnDeviceChanged(MethodRequest methodrequest, object usercontext)
    {
      ShowEventMessage?.Invoke("Receiving cloud to device message OnSensorChanged");
      try
      {
        dynamic payload = JObject.Parse(methodrequest.DataAsJson);

        String bluetoothaddress = payload.bluetoothaddress;
        String action = payload.action;

        // invoke sensor changed delegate
        await (SensorChanged?.Invoke(bluetoothaddress, null, action)).ConfigureAwait(false);

      }
      catch (Exception e)
      {
        // on exception inform iot central => disable/enable sensor
        await AzureIotCentralClient.SendMessageFromDeviceToCloud("alarm", $"Exception OnDeviceChanged {e.Message}  data {methodrequest.DataAsJson}").ConfigureAwait(false);
      }

      return new MethodResponse(200);
    }
    // processes sensor command  from iot central
    private static async Task<MethodResponse> OnSensorChanged(MethodRequest methodrequest, object usercontext)
    {
      ShowEventMessage?.Invoke("Receiving cloud to device message OnSensorChanged");

      try
      {
        // try get ble address, sensortype and action from json string
        dynamic payload = JObject.Parse(methodrequest.DataAsJson);

        String bluetoothaddress = payload.bluetoothaddress;
        String sensortype = payload.sensortype;
        String action = payload.action;

        // invoke sensor changed delegate
        await (SensorChanged?.Invoke(bluetoothaddress, sensortype, action)).ConfigureAwait(false);
      }
      catch (Exception e)
      {
        // on exception inform iot central
        await AzureIotCentralClient.SendMessageFromDeviceToCloud("alarm", $"Exception OnSensorChanged {e.Message} data {methodrequest.DataAsJson}").ConfigureAwait(false);
      }

      return new MethodResponse(200);
    }
    // simulate config command  from iot central
    private static async Task<MethodResponse> OnConfigchanged(MethodRequest methodrequest, object usercontext)
    {
      ShowEventMessage?.Invoke("Receiving cloud to device message OnConfigchanged");
      try
      {
        dynamic payload = JObject.Parse(methodrequest.DataAsJson);
        if (payload.version > Version)
        {
          Version = payload.version;
          ShowEventMessage?.Invoke($"Configuration updated to version {payload.version}");
          await SendMessageFromDeviceToCloud("config", $"Configuration updated to version {payload.version}").ConfigureAwait(false);
        }
        else
        {
          ShowEventMessage?.Invoke($"no update required, already on {Version}");
          await SendMessageFromDeviceToCloud("config", $"no update required, already on {Version}").ConfigureAwait(false);
        }
      }
      catch (Exception e)
      {
        await AzureIotCentralClient.SendMessageFromDeviceToCloud("alarm", $"Exception OnConfigchanged {e.Message} data {methodrequest.DataAsJson}").ConfigureAwait(false);
      }

      return new MethodResponse(200);
    }
    #endregion

    #region disconnect from iot central
    public static async Task DisconnectFromIoTHub()
    {
      // Azure IoT Hub connection
      await _deviceClient.CloseAsync().ConfigureAwait(true);
      _deviceClient.Dispose();
    }
    #endregion

  }
}
