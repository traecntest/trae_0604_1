namespace LiquidCoolingSystem.DataAcquisition.Models;

public class CoolingUnitData
{
    public string UnitId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public double CoolantFlowRate { get; set; }
    public double SupplyTemperature { get; set; }
    public double ReturnTemperature { get; set; }
    public double ColdPlatePressureDrop { get; set; }
    public double PUE { get; set; }
    public double OutdoorWetBulbTemp { get; set; }
    public CDUStatus CDUStatus { get; set; }
    public double CabinetPowerDensity { get; set; }
    public double ChipJunctionTemperature { get; set; }
    public double PumpSpeed { get; set; }
    public double FanFrequency { get; set; }
    public double ComputingLoad { get; set; }
}

public enum CDUStatus
{
    Normal,
    Warning,
    Alarm,
    Offline
}
