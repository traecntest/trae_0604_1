namespace LiquidCoolingSystem.DataAcquisition.Models;

public class OperationLog
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string OperationType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
}
