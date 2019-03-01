using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using Windows.UI.Core;

namespace UwpBluetooth
{
  public static class Helper
  {
    // get blukii beacon temperature as double, from 2 byte, first is pre decimal, second is decimal
    public static double GetBluekiiTemperature(byte[] frameData, int offset)
    {
      byte[] payload = GetPayload(frameData, offset, 2);
      if (payload == null)
      {
        return double.MinValue;
      }

      String temp = $"{(int)payload[0]}{CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0]}{(int)payload[1]}";
      double temperature = Math.Round(double.Parse(temp), 1, MidpointRounding.ToEven);
      return temperature;
    }

    // Get eddystone temperature from 2 bytes, convort to int16 and divide with 256
    public static double GetEddystoneTemperature(byte[] payload, int offset)
    {
      int temperature = ToUint16(payload, offset);
      return temperature != int.MinValue ? temperature / 256d : temperature;
    }

    // Convert 2 bytes to integer, assumes the input is big endian
    public static int ToUint16(byte[] frameData, int offset)
    {
      // Extract 2 bytes at given offset from frame data
      byte[] payload = GetPayload(frameData, offset, 2);
      if (payload == null)
      {
        return int.MinValue;
      }

      // Reverse bytes if platfrom is little endian
      if (BitConverter.IsLittleEndian)
      {
        Array.Reverse(payload);
      }

      // convert to int16
      return BitConverter.ToUInt16(payload, 0);

    }
    // Convert 2 bytes to integer, assumes the input is little endian
    public static int ToUint16FromLittelEndian(byte[] frameData, int offset)
    {
      // Extract 2 bytes at given offset from frame data
      byte[] payload = GetPayload(frameData, offset, 2);
      if (payload == null)
      {
        return int.MinValue;
      }

      // convert to int16
      return BitConverter.ToUInt16(payload, 0);

    }

    // Extracts payload from frame data
    public static byte[] GetPayload(byte[] frameData, int offset, int length)
    {
      if (frameData == null || frameData.Length < offset + length)
      {
        return null;
      }

      byte[] payload = new byte[length];
      Array.Copy(frameData, offset, payload, 0, length);
      return payload;

    }

    // Converts byte array to string for display
    public static String ToHexString(byte[] buffer)
    {
      String result = "";
      foreach (var b in buffer)
      {
        result += $"{b.ToString("X2")} ";
      }

      return result;
    }

    // tries to find the sensor and the action from iot central command data
    public static ActionParameter DecodeSensorCommandAction(IEnumerable<Beacon> beacons, string bluetoothAddressAsString, string sensorAsString, string actionAsString, out string errors)
    {
      // decode BLE address and CommandAction parameter
      ActionParameter actionParameter = DecodeDeviceCommandAction(beacons, bluetoothAddressAsString, actionAsString, out errors);
      if (actionParameter == null)
      {
        return null;
      }

      // check if sensor type arguments are null or empty
      errors = $"Error decoding sensor changed command:{Environment.NewLine}";
      Boolean error = false;
      if (String.IsNullOrWhiteSpace(sensorAsString) == true)
      {
        error = true;
        errors += $"Argument sensor is empty{Environment.NewLine}";
      }

      // decode sensor type
      SensorType sensorType = Helper.DecodeSensorType(sensorAsString);
      if (sensorType == SensorType.None)
      {
        error = true;
        errors += $"Invalid sensor {sensorAsString}{Environment.NewLine}";
      }

      if (error == false)
      {
        // find sensor for sensor type in beacon
        actionParameter.SensorData = actionParameter.Beacon.SensorDatas.FirstOrDefault(s => s.SensorType == sensorType);
        if (actionParameter.SensorData == null)
        {
          error = true;
          errors += $"No sensor for bluetooth address 0x{actionParameter.Beacon.BluetoothAddress:X} sensor type {sensorType.ToString()}{Environment.NewLine}";
        }
        else
        {
          return actionParameter;
        }

      }
      return null;

    }
    // tries to find the beacon and the action from iot central command data
    public static ActionParameter DecodeDeviceCommandAction(IEnumerable<Beacon> beacons, string bluetoothAddressAsString, string actionAsString, out string errors)
    {
      ActionParameter actionParameter = new ActionParameter();

      // check if bluetooth and action arguments are null or empty
      errors = $"Error decoding sensor changed command:{Environment.NewLine}";

      Boolean error = false;
      if (String.IsNullOrWhiteSpace(bluetoothAddressAsString) == true)
      {
        error = true;
        errors += $"Argument bluetooth address is empty{Environment.NewLine}";
      }

      if (String.IsNullOrWhiteSpace(actionAsString) == true)
      {
        error = true;
        errors += $"Argument action is empty{Environment.NewLine}";
      }

      if (error == true)
      {
        return null;
      }

      // get BLE address as ulong
      ulong bluetoothAddress = Helper.DecodeBluetoothAddress(bluetoothAddressAsString);
      if (bluetoothAddress == 0)
      {
        error = true;
        errors += $"Invalid Bluetooth address {bluetoothAddressAsString}{Environment.NewLine}";
      }

      // get CommandAction
      actionParameter.CommandAction = Helper.DecodeAction(actionAsString);
      if (actionParameter.CommandAction == CommandAction.None)
      {
        error = true;
        errors += $"Invalid action {actionAsString}{Environment.NewLine}";
      }

      if (error == false)
      {
        // Find beacon for BLE address
        actionParameter.Beacon = beacons.FirstOrDefault(b => b.BluetoothAddress == bluetoothAddress);
        if (actionParameter.Beacon == null)
        {
          error = true;
          errors += $"No device for bluetooth address 0x{bluetoothAddress:X}{Environment.NewLine}";
        }
        else
        {
          return actionParameter;
        }
      }
      return null;

    }

    // try decode SensorType from string
    public static SensorType DecodeSensorType(string data)
    {
      // enums as list
      var sensorTypes = Enum.GetValues(typeof(SensorType));

      // iterate enums
      foreach (var sensorType in sensorTypes)
      {
        // enum as string
        String text = sensorType.ToString();

        // data longer as enum text
        if (data.Length > text.Length)
        {
          continue;
        }

        if (text.StartsWith(data, StringComparison.OrdinalIgnoreCase) == true)
        {
          return (SensorType)sensorType;
        }
      }

      return SensorType.None;
    }

    // try decode CommandAction from string
    public static CommandAction DecodeAction(string data)
    {
      // enums as list
      var commandActions = Enum.GetValues(typeof(CommandAction));

      // iterate enums
      foreach (var commandAction in commandActions)
      {
        // enum as string
        String text = commandAction.ToString();

        // data longer as enum text
        if (data.Length > text.Length)
        {
          continue;
        }
        // test if data start with enum string
        if (text.StartsWith(data, StringComparison.OrdinalIgnoreCase) == true)
        {
          return (CommandAction)commandAction;
        }
      }

      return CommandAction.None;
    }

    // Convert BLE address string to ulong, try to guess if string is in hex format or not
    public static ulong DecodeBluetoothAddress(string data)
    {
      Boolean hex = false;

      // check if hex string => starts with x
      if (data.StartsWith("x", StringComparison.OrdinalIgnoreCase) == true)
      {
        data = "0" + data;
        hex = true;
      }

      // check if hex string => starts with 0x
      if (data.StartsWith("0x", StringComparison.OrdinalIgnoreCase) == true)
      {
        hex = true;
      }

      // check if hex string => has hex numbers a-f
      if (data.StartsWith("0x", StringComparison.OrdinalIgnoreCase) == false)
      {
        hex = System.Text.RegularExpressions.Regex.IsMatch(data, @"[a-fA-F]");
      }


      // convert to ulong
      try
      {
        if (hex == true)
        {
          return Convert.ToUInt64(data, 16);
        }
        else
        {
          return Convert.ToUInt64(data, 10);
        }

      }
      catch (Exception)
      {
        return 0;
      }

    }

  }

  // Simple delegate command for mvvm, implements ICommand
  public class DelegateCommand : System.Windows.Input.ICommand
  {
    private Action<object> execute;
    private Func<object, bool> canExecute;

    public DelegateCommand(Action<object> execute)
    {
      this.execute = execute;
      this.canExecute = (x) => { return true; };
    }
    public DelegateCommand(Action<object> execute, Func<object, bool> canExecute)
    {
      this.execute = execute;
      this.canExecute = canExecute;
    }
    public bool CanExecute(object parameter)
    {
      return canExecute(parameter);
    }

    public event EventHandler CanExecuteChanged;
    public void RaiseCanExecuteChanged()
    {
      if (CanExecuteChanged != null)
      {
        CanExecuteChanged(this, EventArgs.Empty);
      }
    }
    public void Execute(object parameter)
    {
      execute(parameter);
    }
  }

  // Observable, implements INotifyPropertyChanged
  public class Observable : INotifyPropertyChanged
  {
    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
      var handler = PropertyChanged;
      handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
  }

  // Determines if in ui thread or not
  public static class ThreadHelper
  {
    public static CoreDispatcher Dispatcher { get; set; }
    public static int MainThreadId { get; private set; }

    public static void Initialize(int mainThreadId)
    {
      MainThreadId = mainThreadId;
    }

    public static void Initialize(int mainThreadId, CoreDispatcher dispatcher)
    {
      MainThreadId = mainThreadId;
      Dispatcher = dispatcher;
    }

    public static bool IsOnMainThread => Environment.CurrentManagedThreadId == MainThreadId;

    public static async void RunOnMainThread(Action function)
    {
      if (ThreadHelper.IsOnMainThread)
      {
        function.Invoke();
      }
      else
      {
        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { function(); });
      }
    }
  }
}

