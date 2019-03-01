using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Devices.Bluetooth.Advertisement;

namespace UwpBluetooth
{
  public class MainPageViewModel : Observable
  {
    #region class variables
    private string _alarmMessage = "I feel good";

    private BluetoothLEAdvertisementWatcher Watcher { get; set; }

    // all beacons/device for demo
    public ObservableCollection<Beacon> Beacons { get; set; }
    public ObservableCollection<LogMessage> LogMessages { get; set; } = new ObservableCollection<LogMessage>();
    public ObservableCollection<LogMessage> LogAzureMessages { get; set; } = new ObservableCollection<LogMessage>();

    public ICommand SendAlarmCommand { get; set; }
    public ICommand ExitButtonCommand { get; set; }

    // our demo gatt sensor
    protected GattExample SensorBugGatt { get; set; }

    public Windows.UI.Xaml.Visibility ExitButtonVisibility { get; set; }


    public string AlarmMessage
    {
      get => _alarmMessage;
      set
      {
        _alarmMessage = value;
        OnPropertyChanged();
      }
    }
    #endregion


    #region Constructor
    public MainPageViewModel()
    {
      Package package = Package.Current;
      ExitButtonVisibility = package.Id.Architecture == Windows.System.ProcessorArchitecture.Arm ? Windows.UI.Xaml.Visibility.Visible : Windows.UI.Xaml.Visibility.Collapsed;

      // create ui commands
      SendAlarmCommand = new DelegateCommand(SendAlarmCommandExecute);
      ExitButtonCommand = new DelegateCommand(ExitCommandExecute);
      // call back for AzureIotCentralClient
      AzureIotCentralClient.SensorChanged += OnSensorChanged;
      AzureIotCentralClient.ShowEventMessage += ShowEventMessage;
      AzureIotCentralClient.ShowTelemetryMessage += ShowTelemetryMessage;

      // fill our Beacons list withdemo config
      InitBeacons();
    }

    private void InitBeacons()
    {
      Beacons = new ObservableCollection<Beacon>
      {
        new Beacon
        {
          Name = "Ice blue Beacon",
          Description = "Left side height 1m from floor",
          BluetoothAddress = 0xE832C0E3AA0F,
          BeaconType = BeaconType.EddystoneTlm,
          SensorDatas = new ObservableCollection<SensorData>
          {
            new SensorData {SensorType = SensorType.BatteryMv, Name = "Battery level in mV"},
            new SensorData {SensorType = SensorType.Temperature, Name = "Temperature in ℃"},
          }
        },
        new Beacon
        {
          Name = "Blukii",
          Description = "Middle top 2m from floor",
          BluetoothAddress = 0x2471894DAF5A,
          BeaconType = BeaconType.Blukii,
          SensorDatas = new ObservableCollection<SensorData>
          {
            new SensorData {SensorType = SensorType.BatteryPercent, Name = "Battery level in %"},
            new SensorData {SensorType = SensorType.Temperature, Name = "Temperature in ℃"},
            new SensorData {SensorType = SensorType.Humidity, Name = "Humidity in %"},
            new SensorData {SensorType = SensorType.Luminosity, Name = "Luminosity in lux"},
            new SensorData {SensorType = SensorType.Pressure, Name = "Pressure in dPa"},
          }
        },
      };

      // gatt device is paired to win10 machine
      if (Package.Current.Id.Architecture != Windows.System.ProcessorArchitecture.Arm)
      {
        // TODO RASPI
        Beacon sensorBugBeacon = new Beacon
        {
          Name = "SensorBug",
          Description = "On the floor",
          BluetoothAddress = 0xECFE7E109D7E,
          BeaconType = BeaconType.Unknown,
          SensorDatas = new ObservableCollection<SensorData>
          {
            new SensorData {SensorType = SensorType.BatteryPercent, Name = "Battery level in %"},
            new SensorData {SensorType = SensorType.Temperature, Name = "Temperature in ℃"},
            new SensorData {SensorType = SensorType.Luminosity, Name = "Luminosity in lux"},
          }
        };

        SensorBugGatt = new GattExample(sensorBugBeacon);
        Beacons.Add(sensorBugBeacon);
      }

    }
    #endregion

    #region StartWatching

    // start watching for advertising beacons and gatt devices
    public void StartWatching()
    {
      // connect to iot central
      AzureIotCentralClient.ConnectToIoTCentral();

      // in demo gatt sensor is paired to win10 machine
      if (Package.Current.Id.Architecture != Windows.System.ProcessorArchitecture.Arm)
      {
        SensorBugGatt?.Start();
      }

      // create and start BluetoothLEAdvertisementWatcher
      Watcher = new BluetoothLEAdvertisementWatcher
      {
        ScanningMode = BluetoothLEScanningMode.Active
      };
      Watcher.Received += WatcherOnReceived;

      Watcher.Start();
    }
    #endregion


    #region Receive ardvertising frames
    private async void WatcherOnReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
    {
      // only the configures beacons
      var beacon = Beacons.FirstOrDefault(d => d.BluetoothAddress == args.BluetoothAddress);
      if (beacon == null)
      {
        return;
      }

      Debug.WriteLine($"Name {beacon.Name} BLE address {args.BluetoothAddress:X}");

      // process data sections
      var dataSections = args.Advertisement.DataSections;
      if (dataSections != null)
      {
        foreach (var data in dataSections)
        {
          byte[] payload = data.Data.ToArray();
          Debug.WriteLine($"DataFrame Type 0x{data.DataType:X}  {Helper.ToHexString(data.Data.ToArray())}");

          // is our eddystone beacon
          if (beacon.BeaconType == BeaconType.EddystoneTlm)
          {
            // see eddystone spec, for detecting tlm frame
            if (data.DataType == 0x16 && payload[0] == 0xAA && payload[1] == 0xFE && payload[2] == 0x20)
            {
              // decode sensor values and set in beacon
              int batteryLevel = Helper.ToUint16(payload, 4);
              beacon.SetData(SensorType.BatteryMv, Helper.ToUint16(payload, 4));
              beacon.SetData(SensorType.Temperature, Helper.GetEddystoneTemperature(payload, 6));

              // set timestamp received in UI and update sensor values in UI
              ThreadHelper.RunOnMainThread(() =>
              {
                beacon.TimeStampReceived = args.Timestamp.DateTime;
                beacon.UpdateUi();
              });
              // if all sensor data of device are valid, send to iot central
              if (beacon.AllDataValid() == true)
              {
                await AzureIotCentralClient.SendSensorDataToIotCentral(beacon, args.Timestamp.DateTime).ConfigureAwait(false);
              }
            }
          }

          // is the blukii beacon
          if (beacon.BeaconType == BeaconType.Blukii)
          {
            // detecting environmental frame of blukii => specs in slides
            if (data.DataType == 0xFF && payload.Length >= 22 && payload[14] == 0x10)
            {
              // decode sensor values and set in beacon
              beacon.SetData(SensorType.BatteryPercent, (int)payload[12]);
              beacon.SetData(SensorType.Temperature, Helper.GetBluekiiTemperature(payload, 20));
              beacon.SetData(SensorType.Humidity, (int)payload[19]);
              beacon.SetData(SensorType.Luminosity, Helper.ToUint16(payload, 17));
              beacon.SetData(SensorType.Pressure, Helper.ToUint16(payload, 15));

              // set timestamp received in UI and update sensor values in UI
              ThreadHelper.RunOnMainThread(() =>
              {
                beacon.TimeStampReceived = args.Timestamp.DateTime;
                beacon.UpdateUi();
              });
              // if all sensor data of device are valid, send to iot central
              if (beacon.AllDataValid() == true)
              {
                await AzureIotCentralClient.SendSensorDataToIotCentral(beacon, args.Timestamp.DateTime).ConfigureAwait(false);
              }
            }
          }


        }

        Debug.WriteLine($"");
      }

    }
    #endregion


    #region sensor message from azure iot central 
    // sensor or deviceupdate command from iot central, ble address can be hex or not, sensor is a string temp,humid etc.
    // action is disable or enable
    // if deviceupdate command => sensorAsString == null
    private async Task OnSensorChanged(string bluetoothAddressAsString, string sensorAsString, string actionAsString)
    {
      String errors = "";
      if (sensorAsString == null)
      {
        // try get ble address as ulong, action as CommandAction enum
        ActionParameter actionParameterDevice = Helper.DecodeDeviceCommandAction(Beacons, bluetoothAddressAsString,
          actionAsString, out errors);
        if (actionParameterDevice == null)
        {
          // something is wrong, inform iot central
          await AzureIotCentralClient.SendMessageFromDeviceToCloud("alarm", errors).ConfigureAwait(false);
        }
        else
        {
          // perform enable/disable action on beacon
          actionParameterDevice.Beacon.PerformAction(actionParameterDevice.CommandAction);
        }
      }
      else
      {
        // try get ble address as ulong, sensor as SensorData, action as CommandAction enum
        ActionParameter actionParameter = Helper.DecodeSensorCommandAction(Beacons, bluetoothAddressAsString, sensorAsString,
          actionAsString, out errors);
        if (actionParameter == null)
        {
          // something is wrong, inform iot central
          await AzureIotCentralClient.SendMessageFromDeviceToCloud("alarm", errors).ConfigureAwait(false);
        }
        else
        {
          // perform enable/disable action on sensor
          actionParameter.SensorData.PerformAction(actionParameter.CommandAction);
        }
      }
    }
    #endregion

    #region exceute demo alarm
    private async void SendAlarmCommandExecute(object obj)
    {
      // TODO V2
      await AzureIotCentralClient.SendMessageFromDeviceToCloud("alarm", AlarmMessage).ConfigureAwait(false);
    }
    #endregion

    #region show event messages

    // show message in ui right listview
    public void ShowTelemetryMessage(string message)
    {
      ThreadHelper.RunOnMainThread(() =>
      {
        LogMessages.Insert(0, new LogMessage
        {
          MessageText = message,
          Time = DateTime.Now.ToString("HH:mm:ss")
        });
      });

      Debug.WriteLine(message);

    }
    // show message in ui left listview
    public void ShowEventMessage(string message)
    {
      ThreadHelper.RunOnMainThread(() =>
      {
        LogAzureMessages.Insert(0, new LogMessage
        {
          MessageText = message,
          Time = DateTime.Now.ToString("HH:mm:ss")
        });
      });

      Debug.WriteLine(message);

    }
    #endregion

    #region ExitCommandExecute
    // exit button on windows iot core
    private void ExitCommandExecute(object obj)
    {
      CoreApplication.Exit();
    }
    #endregion
  }




}
