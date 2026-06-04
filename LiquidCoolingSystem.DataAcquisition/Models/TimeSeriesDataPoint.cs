namespace LiquidCoolingSystem.DataAcquisition.Models;

public class TimeSeriesDataPoint
{
    public string Measurement { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public Dictionary<string, string> Tags { get; set; } = new();
    public Dictionary<string, object> Fields { get; set; } = new();
}
