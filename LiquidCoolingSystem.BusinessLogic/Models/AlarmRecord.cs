namespace LiquidCoolingSystem.BusinessLogic.Models;

public class AlarmRecord
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string UnitId { get; set; } = string.Empty;
    public AlarmLevel Level { get; set; }
    public AlarmType Type { get; set; }
    public string Message { get; set; } = string.Empty;
    public double CurrentValue { get; set; }
    public double Threshold { get; set; }
    public bool Acknowledged { get; set; }
    public string? AcknowledgedBy { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
}

public enum AlarmLevel
{
    Info,
    Warning,
    Critical,
    Emergency
}

public enum AlarmType
{
    PowerDensity,
    TemperatureDelta,
    ChipTemperature,
    PUE,
    PressureDrop,
    FlowRate,
    CDUStatus
}
