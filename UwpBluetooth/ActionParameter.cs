using System;



namespace UwpBluetooth
{
  // class with parameters for iot central demo commands
  public class ActionParameter
  {
    // the beacon to execute this action
    public Beacon Beacon { get; set; }

    // the SensorData to execute this action, can be null => action is executed on alls SensorData of beacon
    public SensorData SensorData { get; set; }

    // the action to execute 
    public CommandAction CommandAction { get; set; } = CommandAction.None;
  }

  public enum CommandAction { None = -1, New = 0, Update = 1, Enable = 2, Disable = 3 }



}
