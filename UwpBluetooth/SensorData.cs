using System;
using Windows.UI;
using Windows.UI.Xaml.Media;

namespace UwpBluetooth
{
  // represensts one sensor
  public class SensorData : Observable
  {
    #region declaration class variables
    private double _value = double.MinValue;
    private bool _enabled = true;
    private Brush _brushEnabledDisabled = new SolidColorBrush(Colors.LightSeaGreen);

    // Value in data, can be set from any thread
    public double ValueReceived { get; set; }

    // asensor type
    public SensorType SensorType { get; set; }

    // Name of sensor in UI
    public String Name { get; set; }

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

    // if sensor enabled, if not enabled no data processing and ui color changed
    public Boolean Enabled
    {
      get => _enabled;
      set
      {
        if (_enabled != value)
        {
          _enabled = value;
          BrushEnabledDisabled = _enabled == true ? BrushEnabled : BrushDisabled;
          if (_enabled == false)
          {
            ValueReceived = Value = double.MinValue;
          }
          OnPropertyChanged();
        }
      }
    }

    // Value in UI
    public double Value
    {
      get => _value;
      set
      {
        if (Math.Abs(_value - value) > 0.01f)
        {
          _value = value;
          OnPropertyChanged();
        }
      }
    }
    #endregion

    // Updates UI Value, can only be called from UI thread
    public void UpdateUi()
    {
      Value = ValueReceived;
    }

    // performs iot central action enable/disable
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
    }
  }

  // enum of all sensor types in demo
  public enum SensorType { None = -1, Temperature = 0, Humidity = 1, Pressure = 2, Luminosity = 3, BatteryMv = 4, BatteryPercent = 5 }
}
