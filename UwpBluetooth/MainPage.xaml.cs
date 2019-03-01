using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// Die Elementvorlage "Leere Seite" wird unter https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x407 dokumentiert.

namespace UwpBluetooth
{
  /// <summary>
  /// Eine leere Seite, die eigenständig verwendet oder zu der innerhalb eines Rahmens navigiert werden kann.
  /// </summary>
  public sealed partial class MainPage : Page
  {
    // view model for step 3
    private MainPageViewModel MainPageViewModel { get; set; }

    // watcher for receiving advertisement frames
    private BluetoothLEAdvertisementWatcher Watcher { get; set; }

    // BLE addresses of demo beacons
    public ulong IceBlueBeaconBleAddress { get;  } = 0xE8_32_C0_E3_AA_0F; //255304682154511;
    public ulong BlukiiBeaconBleAddress { get;  } = 0x24_71_89_4D_AF_5A; //40070053474138;

    // List of active beacons bluetooth addresses
    private List<ulong> ActiveBeacons { get; set; } = new List<ulong>();

    // demo Gatt device
    private GattExample GattExample { get; set; } = new GattExample();

    public MainPage()
    {
      ThreadHelper.Initialize(Environment.CurrentManagedThreadId, this.Dispatcher);
 
      this.InitializeComponent();

      // Fill list of active beacons
      ActiveBeacons.Add(IceBlueBeaconBleAddress);
      ActiveBeacons.Add(BlukiiBeaconBleAddress);

      // Create viewmodel and set the data context
      DataContext = MainPageViewModel = new MainPageViewModel();

    }



 
    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
      // 1 receive GATT sensor data
      //GattExample.Start();

      // 2 Receive dvertising beacon sensor data and display in debug window
      //Watcher = new BluetoothLEAdvertisementWatcher
      //{
      //  ScanningMode = BluetoothLEScanningMode.Active
      //};
      //Watcher.Received += WatcherOnReceived;
      //Watcher.Start();


      // 3 receive GATT and advertising beacon sensor data and display in UI 
      MainPageViewModel.StartWatching();

    }

    // callback receiving advertising frames
    private void WatcherOnReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
    {
      // Only "our" beacons
      if (ActiveBeacons.Count(b => b == args.BluetoothAddress) == 0)
      {
        return;
      }

      Debug.WriteLine($"BLE address {args.BluetoothAddress:X}");

      // iterate data sections
      var dataSections = args.Advertisement.DataSections;
      if (dataSections != null)
      {
        foreach (var data in dataSections)
        {
          byte[] payload = data.Data.ToArray();
          Debug.WriteLine($"DataFrame Type 0x{data.DataType:X}  {Helper.ToHexString(data.Data.ToArray())}");

          // IS our eddystone telemetry beacon
          if (args.BluetoothAddress == IceBlueBeaconBleAddress)
          {
            // see eddystone spec, for detecting tlm frame
            if (data.DataType == 0x16 && payload[0] == 0xAA && payload[1] == 0xFE && payload[2] == 0x20)
            {
              // decode sensor values
              int batteryLevel = Helper.ToUint16(payload, 4);
              int temp = Helper.ToUint16(payload, 6);
              double temperature = temp != int.MinValue ? temp / 256d : temp;
              Debug.WriteLine($"Received {args.Timestamp.DateTime} battery {batteryLevel} mV {temperature} ℃ Signal strength {args.RawSignalStrengthInDBm} dbm");
            }
          }

          // is the blukii beacon
          if (args.BluetoothAddress == BlukiiBeaconBleAddress)
          {
            // detecting environmental frame of blukii => specs in slides
            if (data.DataType == 0xFF && payload.Length >= 22 && payload[14] == 0x10)
            {
              // decode sensor values
              int batteryPercent = (int)payload[12];
              double temperature = Helper.GetBluekiiTemperature(payload, 20);
              int humidity = (int)payload[19];
              double luminosity = Helper.ToUint16(payload, 17);
              int pressure = Helper.ToUint16(payload, 15);
              Debug.WriteLine($"Blukii Received {args.Timestamp.DateTime} battery {batteryPercent} % {temperature} ℃ {Environment.NewLine}humidity {humidity} % Lux {luminosity} lx pressure {pressure} hpa signal strength {args.RawSignalStrengthInDBm} dbm");
            }
          }
        }
      }


      Debug.WriteLine($"");
    }

  }

}
