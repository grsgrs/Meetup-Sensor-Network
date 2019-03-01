using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Xaml.Media;

namespace UwpBluetooth
{
  // represents on beacon/ble device with n sensors
  public class Beacon : Observable
  {
    #region declaration class variables
    private bool _enabled = true;
    private Brush _brushEnabledDisabled = new SolidColorBrush(Colors.LightSeaGreen);
    private DateTime _timeStampReceived;

    public string Name { get; set; }
    public string Description { get; set; }

    // type fo beacon, is used in code to process data by bluetooth address
    public BeaconType BeaconType { get; set; } = BeaconType.Unknown;

    // BLE addres of device
    public ulong BluetoothAddress { get; set; }

    // collection of sensor data
    public ObservableCollection<SensorData> SensorDatas { get; set; }

    public Brush BrushEnabled { get; set; } = new SolidColorBrush(Colors.LightSeaGreen);
    public Brush BrushDisabled { get; set; } = new SolidColorBrush(Colors.DarkGray);

    public Brush BrushEnabledDisabled
    {
      get => _brushEnabledDisabled;
      set
      {
        if (_brushEnabledDisabled != value)
        {
          _brushEnabledDisabled = value;
          OnPropertyChanged();
        }
      }
    }

    // is beacon enabled, if not enabled no data processing and ui color changed
    public Boolean Enabled
    {
      get => _enabled;
      set
      {
        if (_enabled != value)
        {
          _enabled = value;
          BrushEnabledDisabled = _enabled == true ? BrushEnabled : BrushDisabled;
          OnPropertyChanged();
        }
      }
    }

    // timestamp data received
    public DateTime TimeStampReceived
    {
      get => _timeStampReceived;
      set
      {
        _timeStampReceived = value;
        OnPropertyChanged();
      }
    }
    #endregion

    // sets the value by sensor type
    public void SetData(SensorType sensorType, double value)
    {
      SensorData sensorData = SensorDatas.FirstOrDefault(s => s.SensorType == sensorType);
      if (sensorData != null && sensorData.Enabled == true)
      {
        sensorData.ValueReceived = value;
      }
    }

    // Updates UI Value, can only be called from UI thread
    public void UpdateUi()
    {
      foreach (var sensorData in SensorDatas)
      {
        sensorData.UpdateUi();
      }
    }

    // Perform enable/disable action from iot central
    public void PerformAction(CommandAction action)
    {
      switch (action)
      {
        case CommandAction.Disable:
          ThreadHelper.RunOnMainThread(() =>
          {
            Enabled = false;
          });
          break;
        case CommandAction.Enable:
          ThreadHelper.RunOnMainThread(() =>
          {
            Enabled = true;
          });
          break;
        default:
          break;
      }

      foreach (var sensor in SensorDatas)
      {
        sensor.PerformAction(action);
      }
    }

    // check if all sensors of device have data
    public bool AllDataValid()
    {
      foreach (var sensor in SensorDatas)
      {
        if (sensor.Value - double.MinValue < 0.001f)
        {
          return false;
        }
      }

      return true;
    }
  }

  // beacon type, two types for demo
  public enum BeaconType { Unknown, EddystoneTlm, Blukii }
}
