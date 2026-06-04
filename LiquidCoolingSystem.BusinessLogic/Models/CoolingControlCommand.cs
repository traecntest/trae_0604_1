namespace LiquidCoolingSystem.BusinessLogic.Models;

public class CoolingControlCommand
{
    public string UnitId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public double TargetPumpSpeed { get; set; }
    public double TargetFanFrequency { get; set; }
    public double TargetSupplyTemperature { get; set; }
    public string ControlReason { get; set; } = string.Empty;
    public double ComputedLoad { get; set; }
}
