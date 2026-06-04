using LiquidCoolingSystem.BusinessLogic.Models;
using LiquidCoolingSystem.DataAcquisition.Models;

namespace LiquidCoolingSystem.BusinessLogic.Interfaces;

public interface IAlarmEngine
{
    event EventHandler<AlarmRecord>? AlarmTriggered;

    Task AnalyzeDataAsync(CoolingUnitData data);
    Task<IEnumerable<AlarmRecord>> GetActiveAlarmsAsync();
    Task<IEnumerable<AlarmRecord>> GetAlarmHistoryAsync(DateTime startTime, DateTime endTime);
    Task AcknowledgeAlarmAsync(long alarmId, string acknowledgedBy);
    void ConfigureThresholds(AlarmThresholds thresholds);
}

public class AlarmThresholds
{
    public double WarningPowerDensity { get; set; } = 40;
    public double CriticalPowerDensity { get; set; } = 48;
    public double WarningTempDelta { get; set; } = 12;
    public double CriticalTempDelta { get; set; } = 15;
    public double WarningChipTemp { get; set; } = 80;
    public double CriticalChipTemp { get; set; } = 88;
    public double WarningPUE { get; set; } = 1.25;
    public double CriticalPUE { get; set; } = 1.35;
}
