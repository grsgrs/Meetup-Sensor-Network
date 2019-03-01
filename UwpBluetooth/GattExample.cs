using System;
using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;
using Windows.UI.Xaml;

namespace UwpBluetooth
{
  #region GattExample
  public class GattExample
  {
    #region class variables
    // ble device watcher
    private DeviceWatcher DeviceWatcher { get; set; }

    // Timer for reading sensor values
    private DispatcherTimer Timer { get; set; }

    // bluetooth address for SensorBug device in demo
    public ulong SensorBugBluetoothAddress { get; } = 0xEC_FE_7E_10_9D_7E;

    // Gatt uuids for temperaure
    public String TemperatureGattServiceUuid { get; } = "9dc84838-7619-4f09-a1ce-ddcf63225b30";
    public String TemperatureGattServiceCharateristicDataUuid { get; } = "9dc84838-7619-4f09-a1ce-ddcf63225b32";
    public String TemperatureGattServiceCharateristicConfigUuid { get; } = "9dc84838-7619-4f09-a1ce-ddcf63225b31";
    // Gatt service for temperaure
    public GattDeviceService GattDeviceServiceTemperature { get; set; }

    // Gatt uuids for luminosity
    public String LuminosityGattServiceUuuid { get; } = "9dc84838-7619-4f09-a1ce-ddcf63225b20";
    public String LuminosityGattServiceConfigCharateristicUuuid { get; } = "9dc84838-7619-4f09-a1ce-ddcf63225b21";
    public String LuminosityGattServiceDataCharateristicUuuid { get; } = "9dc84838-7619-4f09-a1ce-ddcf63225b22";
    // Gatt service for luminosity
    public GattDeviceService GattDeviceServiceLuminosity { get; set; }

    // Gatt uuids for battery
    public String BatteryGattServiceUuuid { get; } = "0000180F-0000-1000-8000-00805F9B34FB";
    public String BatteryGattServiceDataCharateristicUuuid { get; } = "00002A19-0000-1000-8000-00805F9B34FB";
    // Gatt service for battery
    public GattDeviceService GattDeviceServiceBattery { get; set; }

    // 
    private SemaphoreSlim SemaphoreSlim { get; set; } = new SemaphoreSlim(1,1);

    protected Beacon Beacon { get; set; }
    #endregion

    #region constructors
    public GattExample()
    {
    }


    // constructor with Beacon argument, if set this beacon will receive values later
    public GattExample(Beacon beacon)
    {
      Beacon = beacon;

      // Start timer for reading sensor values
      Timer = new DispatcherTimer();
      Timer.Interval = TimeSpan.FromSeconds(30);
      Timer.Tick += Timer_Tick;
    }
    #endregion

    #region start
    // start watch for devices
    public void Start()
    {
      // Only paired devices
      string[] requestedProperties = {"System.Devices.Aep.IsPaired"};

      //Protocoll ID for bluetooth LE devices
      string aqsFilter = "System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\"";

      // Create device watchaer
      DeviceWatcher = DeviceInformation.CreateWatcher(
        aqsFilter,
        requestedProperties,
        DeviceInformationKind.AssociationEndpoint
      );

      // Set event handler for receiving device informations
      DeviceWatcher.Added += DeviceWatcher_Added;

      // start watching for devices
      DeviceWatcher.Start();

    }

    // callback from device watcher
    private async void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation deviceInformation)
    {
      // Get reference on BluetoothLEDevice by Id from deviceInformation
      BluetoothLEDevice bluetoothLeDevice = await BluetoothLEDevice.FromIdAsync(deviceInformation.Id);

      // Check if this ist the device we want => SensorBug109D7
      if (bluetoothLeDevice?.BluetoothAddress != SensorBugBluetoothAddress)
      {
        return;
      }

      // device information received
      SemaphoreSlim.Wait();
      await TryLocateServices(bluetoothLeDevice).ConfigureAwait(true);
      SemaphoreSlim.Release();
    }

    public async Task<Boolean> TryLocateServices(BluetoothLEDevice bluetoothLeDevice)
    {

      // Only one init
      if (GattDeviceServiceTemperature != null || GattDeviceServiceLuminosity != null || GattDeviceServiceBattery != null)
      {
        return true;
      }

      // Try get access to device
      Windows.Devices.Enumeration.DeviceAccessStatus result;
      try
      {
        result = await bluetoothLeDevice.RequestAccessAsync();
      }
      catch (Exception)
      {
        result = Windows.Devices.Enumeration.DeviceAccessStatus.DeniedBySystem;
      }

      // Access denied
      if (result != DeviceAccessStatus.Allowed)
      {
        return false;
      }

      // Get services of device in loop with timeout of 100 ms 
      GattDeviceServicesResult gattDeviceServicesResult = null;
      for (int i = 0; i < 5; i++)
      {
        // Try get list of services
        gattDeviceServicesResult = await bluetoothLeDevice.GetGattServicesAsync(BluetoothCacheMode.Uncached);

        // Any services in list
        if (gattDeviceServicesResult?.Services.Count > 0)
        {
          break;
        }

        // Wait 100 ms
        await Task.Delay(100).ConfigureAwait(false);
      }

      // No services available
      if (gattDeviceServicesResult == null || gattDeviceServicesResult?.Services.Count == 0)
      {
        return false;
      }

      // Iterate services 
      foreach (var service in gattDeviceServicesResult.Services)
      {
        // 9dc84838-7619-4f09-a1ce-ddcf63225b20 is the luminosity service
        if (String.Compare(LuminosityGattServiceUuuid, service.Uuid.ToString(),
              StringComparison.OrdinalIgnoreCase) == 0)
        {
          GattDeviceServiceLuminosity = service;
        }

        if (String.Compare(BatteryGattServiceUuuid, service.Uuid.ToString(),
              StringComparison.OrdinalIgnoreCase) == 0)
        {
          GattDeviceServiceBattery = service;
        }

        if (String.Compare(TemperatureGattServiceUuid, service.Uuid.ToString(),
              StringComparison.OrdinalIgnoreCase) == 0)
        {
          GattDeviceServiceTemperature = service;
        }
      }

      double batteryLevel = await ReadBatteryValue().ConfigureAwait(true);
      double lux = await ReadLuminosityValue().ConfigureAwait(true);
      Debug.WriteLine($"SensorBug Lux {lux} lx battery level {batteryLevel} %");

      await ReadValues().ConfigureAwait(true);

      // we can read values => start imer
      ThreadHelper.RunOnMainThread(() =>
      {
        Timer?.Start();
      });


      return true;
    }
    #endregion

    #region receive values luminosity temperature battery
    public async Task<double> ReadLuminosityValue()
    {
      if (GattDeviceServiceLuminosity == null)
      {
        return double.MinValue;
      }

      return await ReadValue(GattDeviceServiceLuminosity, LuminosityGattServiceConfigCharateristicUuuid,
        LuminosityGattServiceDataCharateristicUuuid, new ValueConverterLuminosityCcSensorBug109D7E()).ConfigureAwait(false);

    }

    public async Task<double> ReadTemperatureValue()
    {
      if (GattDeviceServiceTemperature == null)
      {
        return double.MinValue;
      }

      return await ReadValue(GattDeviceServiceTemperature, TemperatureGattServiceCharateristicConfigUuid,
        TemperatureGattServiceCharateristicDataUuid, new ValueConverterTemperatureSensorBug109D7E()).ConfigureAwait(false);

    }
    public async Task<double> ReadBatteryValue()
    {
      if (GattDeviceServiceBattery == null)
      {
        return double.MinValue;
      }
      return await ReadValue(GattDeviceServiceBattery, BatteryGattServiceDataCharateristicUuuid).ConfigureAwait(false);
    
    }

    #region read value generic
    protected async Task<double> ReadValue(GattDeviceService deviceService, string gattConfigCharateristicUuuid,
      String gattDataCharateristicUuuid, ValueConverterBase valueConverter)
    {
      if (deviceService == null || valueConverter == null)
      {
        return double.MinValue;
      }
      try
      {
        double sensorValue = double.MinValue;

        // Get characteristic for config of luminosity sensor
        GattCharacteristic configCharacteristic = (await deviceService.GetCharacteristicsForUuidAsync(
          new Guid(gattConfigCharateristicUuuid))).Characteristics[0];


        // Enable sensor
        byte[] enable = { 1 };
        await configCharacteristic.WriteValueAsync(enable.AsBuffer());

        // Get characteristic for reading of sensor
        GattCharacteristic dataCharacteristic = (await deviceService.GetCharacteristicsForUuidAsync(
          new Guid(gattDataCharateristicUuuid))).Characteristics[0];

        // try read in loop
        for (int i = 0; i < 5; i++)
        {
          // Read value from data characteristic
          GattReadResult readResult = await dataCharacteristic.ReadValueAsync(BluetoothCacheMode.Uncached);

          // Valid data ?
          if (readResult.Value != null && readResult.Value.Length > 0)
          {
            // Convert data from IBuffer to byte[]
            byte[] data = new byte[readResult.Value.Length];
            DataReader.FromBuffer(readResult.Value).ReadBytes(data);

            // Use the value converter for luminosity of this sensor
            valueConverter.SetConfigCharacteristic(configCharacteristic);
            sensorValue = valueConverter.GetValue(data);
            break;
          }

          // Wait for 100 ms
          await Task.Delay(100).ConfigureAwait(true);
        }


        byte[] disable = { 0 };
        await configCharacteristic.WriteValueAsync(disable.AsBuffer(), GattWriteOption.WriteWithoutResponse);

        return sensorValue;

      }
      catch (Exception e)
      {
        Debug.WriteLine(e);
        return double.MinValue;
      }
    }


    // read value without enable/disable sensor
    protected static async Task<double> ReadValue(GattDeviceService deviceService, String gattDataCharateristicUuuid)
    {
      try
      {
        double sensorValue = double.MinValue;

        // Get characteristic for reading of battery level
        GattCharacteristic dataCharacteristic = (await deviceService.GetCharacteristicsForUuidAsync(
          new Guid(gattDataCharateristicUuuid))).Characteristics[0];

        // try read in loop
        for (int i = 0; i < 5; i++)
        {
          // Read value from data characteristic
          GattReadResult readResult = await dataCharacteristic.ReadValueAsync(BluetoothCacheMode.Uncached);

          // Valid data ?
          if (readResult.Value != null && readResult.Value.Length > 0)
          {
            // Convert data from IBuffer to byte[]
            byte[] data = new byte[readResult.Value.Length];
            DataReader.FromBuffer(readResult.Value).ReadBytes(data);

            // Create a vlaue converter for luminosity of this sensor
            sensorValue = (double)data[0];
            break;
          }

          // Wait for 100 ms
          await Task.Delay(100).ConfigureAwait(true);
        }


        return sensorValue;

      }
      catch (Exception e)
      {
        Debug.WriteLine(e);
        return double.MinValue;
      }
    }
    #endregion

    #region read values for device
    private async Task ReadValues()
    {
      if (Beacon == null)
      {
        return;
      }

      // read values fromm sensor
      DateTime timeStamp = DateTime.Now;
      double batteryLevel = await ReadBatteryValue().ConfigureAwait(true);
      double lux = await ReadLuminosityValue().ConfigureAwait(true);
      double temperature = await ReadTemperatureValue().ConfigureAwait(true);

      if (Beacon == null)
      {
        return;
      }

      // set values in beacon
      Beacon?.SetData(SensorType.BatteryPercent, batteryLevel);
      Beacon?.SetData(SensorType.Luminosity, lux);
      Beacon?.SetData(SensorType.Temperature, temperature);

      // do the ui stuff
      ThreadHelper.RunOnMainThread(() =>
      {
        Beacon.TimeStampReceived = timeStamp;
        Beacon?.UpdateUi();
      });
 
      // send to iot central if all sensor data are valid
      if (Beacon?.AllDataValid() == true)
      {
        await AzureIotCentralClient.SendSensorDataToIotCentral(Beacon, timeStamp).ConfigureAwait(false);
      }
    }
    #endregion
    #endregion

    #region Timer_Tick
    // timer tick, read sensor values every tick
    private async void Timer_Tick(object sender, object e)
    {
      await ReadValues().ConfigureAwait(false);
    }
    #endregion
  }
  #endregion
  #region helper classes

  #region ValueConverterLuminosityCcSensorBug109D7E
  // sensor bug uses value ranges, the first byte reprensents the range
  public class ValueConverterLuminosityCcSensorBug109D7E : ValueConverterBase
  {
    private byte[] ConfigData { get; set; }
    private GattCharacteristic ConfigCharacteristic { get; set; }
    private readonly uint[] _rangeVal = { 1000, 4000, 16000, 64000 };
    private readonly uint[] _resolutionMaxVal = { 65535, 4095, 255, 15 };
    int DataBytesCount { get; set; }
    int IndexRange { get; set; }
    int IndexResolution { get; set; }
    Boolean ConfigSet { get; set; }

    public override void SetConfigCharacteristic(GattCharacteristic configCharacteristic)
    {
      ConfigCharacteristic = configCharacteristic;
      if (ConfigCharacteristic != null && ConfigSet == false)
      {
        GattReadResult readResult = null;

        Task.Run(async () => { readResult = await ConfigCharacteristic.ReadValueAsync(BluetoothCacheMode.Uncached); })
          .Wait();

        if (readResult.Value != null && readResult.Value.Length > 0)
        {
          ConfigData = new byte[readResult.Value.Length];
          DataReader.FromBuffer(readResult.Value).ReadBytes(ConfigData);

          ConfigData[14] = 3;
          ConfigData[15] = 0;
          Task.Run(async () => { await configCharacteristic.WriteValueAsync(ConfigData.AsBuffer()); }).Wait();
          ConfigSet = true;

        }

      }

    }

    void DecodeConfig(byte config)
    {
      BitArray bitArray = new BitArray(new[] { config });
      if (bitArray[0])
      {
        DataBytesCount |= 0b0000001;
      }

      if (bitArray[1])
      {
        DataBytesCount |= 0b0000010;
      }

      if (bitArray[2])
      {
        IndexRange |= 0b0000001;
      }

      if (bitArray[3])
      {
        IndexRange |= 0b0000010;
      }

      if (bitArray[4])
      {
        IndexResolution |= 0b0000001;
      }

      if (bitArray[5])
      {
        IndexResolution |= 0b0000010;
      }

    }

    public override double GetValue(byte[] bArray)
    {
      if (CheckBytes(2, bArray) == false)
      {
        return double.MinValue;
      }

      try
      {
        // In the first byte is actual configuration
        DecodeConfig(bArray[0]);
        byte[] dest = new byte[2];
        Array.Copy(bArray, 1, dest, 0, DataBytesCount);

        uint value = BitConverter.ToUInt16(dest, 0);
        return Math.Round(value * (double)_rangeVal[IndexRange] / _resolutionMaxVal[IndexResolution]);
      }
      catch (Exception exception)
      {
        WriteMessage($"{GetType().Name} exception {exception.Message}");
        return double.MinValue;
      }
    }
  }
  #endregion

  #region ValueConverterTemperatureSensorBug109D7E
  // sensorbug calculates the temperature from int16
  public class ValueConverterTemperatureSensorBug109D7E : ValueConverterBase
  {

    public override double GetValue(byte[] bArray)
    {
      if (CheckBytes(1, bArray) == false)
      {
        return double.MinValue;
      }

      return Math.Round((((double)BitConverter.ToInt16(bArray, 0)) / 10) * 0.625, 1);
    }
  }
  #endregion

  #region ValueConverterBase
  // value converters base class
  public class ValueConverterBase
  {

    public virtual Boolean CheckBytes(int bytesExpected, byte[] bArray)
    {
      if (bArray == null)
      {
        WriteMessage(
          "byte array is null");
        return false;
      }

      if (bArray.Length < bytesExpected)
      {
        WriteMessage(
          $"expected {bytesExpected} bytes received  {bArray.Length} bytes ({Helper.ToHexString(bArray)})");
        return false;
      }

      return true;
    }

    public void WriteMessage(string message)
    {
      Debug.WriteLine(message);
    }


    public virtual void SetConfigCharacteristic(GattCharacteristic configCharacteristic)
    {
    }

    public virtual double GetValue(byte[] bArray)
    {
      return double.MinValue;
    }
  }
  #endregion

 
  #endregion
}
