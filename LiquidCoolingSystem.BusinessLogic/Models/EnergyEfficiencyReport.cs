namespace LiquidCoolingSystem.BusinessLogic.Models;

public class EnergyEfficiencyReport
{
    public DateTime ReportTime { get; set; }
    public TimeSpan Duration { get; set; }
    public double AveragePUE { get; set; }
    public double MinPUE { get; set; }
    public double MaxPUE { get; set; }
    public double TotalEnergySaved { get; set; }
    public double CoolingEfficiency { get; set; }
    public Dictionary<string, UnitEfficiency> UnitEfficiencies { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}

public class UnitEfficiency
{
    public string UnitId { get; set; } = string.Empty;
    public double AveragePUE { get; set; }
    public double EnergyConsumption { get; set; }
    public double CoolingEnergy { get; set; }
}
