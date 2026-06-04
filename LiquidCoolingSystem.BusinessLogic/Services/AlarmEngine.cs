using LiquidCoolingSystem.BusinessLogic.Interfaces;
using LiquidCoolingSystem.BusinessLogic.Models;
using LiquidCoolingSystem.DataAcquisition.Models;
using Microsoft.Extensions.Logging;

namespace LiquidCoolingSystem.BusinessLogic.Services;

public class AlarmEngine : IAlarmEngine
{
    private readonly ILogger<AlarmEngine> _logger;
    private readonly List<AlarmRecord> _alarmHistory;
    private AlarmThresholds _thresholds;
    private long _nextAlarmId;

    public event EventHandler<AlarmRecord>? AlarmTriggered;

    public AlarmEngine(ILogger<AlarmEngine> logger)
    {
        _logger = logger;
        _alarmHistory = new List<AlarmRecord>();
        _thresholds = new AlarmThresholds();
        _nextAlarmId = 1;
    }

    public async Task AnalyzeDataAsync(CoolingUnitData data)
    {
        var tempDelta = data.ReturnTemperature - data.SupplyTemperature;

        await CheckPowerDensityAsync(data);
        await CheckTemperatureDeltaAsync(data, tempDelta);
        await CheckChipTemperatureAsync(data);
        await CheckPUEAsync(data);
        await CheckCDUStatusAsync(data);

        _logger.LogDebug("Alarm analysis completed for unit {UnitId}", data.UnitId);
    }

    private Task CheckPowerDensityAsync(CoolingUnitData data)
    {
        if (data.CabinetPowerDensity > _thresholds.CriticalPowerDensity)
        {
            TriggerAlarm(data, AlarmLevel.Emergency, AlarmType.PowerDensity,
                $"机柜功率密度严重超标: {data.CabinetPowerDensity:F1} kW/rack",
                data.CabinetPowerDensity, _thresholds.CriticalPowerDensity);
        }
        else if (data.CabinetPowerDensity > _thresholds.WarningPowerDensity)
        {
            TriggerAlarm(data, AlarmLevel.Critical, AlarmType.PowerDensity,
                $"机柜功率密度超标: {data.CabinetPowerDensity:F1} kW/rack",
                data.CabinetPowerDensity, _thresholds.WarningPowerDensity);
        }
        return Task.CompletedTask;
    }

    private Task CheckTemperatureDeltaAsync(CoolingUnitData data, double tempDelta)
    {
        if (tempDelta > _thresholds.CriticalTempDelta)
        {
            TriggerAlarm(data, AlarmLevel.Emergency, AlarmType.TemperatureDelta,
                $"冷却水温差异常: {tempDelta:F1}°C", tempDelta, _thresholds.CriticalTempDelta);
        }
        else if (tempDelta > _thresholds.WarningTempDelta)
        {
            TriggerAlarm(data, AlarmLevel.Warning, AlarmType.TemperatureDelta,
                $"冷却水温差偏高: {tempDelta:F1}°C", tempDelta, _thresholds.WarningTempDelta);
        }
        return Task.CompletedTask;
    }

    private Task CheckChipTemperatureAsync(CoolingUnitData data)
    {
        if (data.ChipJunctionTemperature > _thresholds.CriticalChipTemp)
        {
            TriggerAlarm(data, AlarmLevel.Emergency, AlarmType.ChipTemperature,
                $"芯片结温严重超标: {data.ChipJunctionTemperature:F1}°C",
                data.ChipJunctionTemperature, _thresholds.CriticalChipTemp);
        }
        else if (data.ChipJunctionTemperature > _thresholds.WarningChipTemp)
        {
            TriggerAlarm(data, AlarmLevel.Critical, AlarmType.ChipTemperature,
                $"芯片结温偏高: {data.ChipJunctionTemperature:F1}°C",
                data.ChipJunctionTemperature, _thresholds.WarningChipTemp);
        }
        return Task.CompletedTask;
    }

    private Task CheckPUEAsync(CoolingUnitData data)
    {
        if (data.PUE > _thresholds.CriticalPUE)
        {
            TriggerAlarm(data, AlarmLevel.Warning, AlarmType.PUE,
                $"PUE值偏高: {data.PUE:F3}", data.PUE, _thresholds.CriticalPUE);
        }
        return Task.CompletedTask;
    }

    private Task CheckCDUStatusAsync(CoolingUnitData data)
    {
        if (data.CDUStatus == CDUStatus.Alarm)
        {
            TriggerAlarm(data, AlarmLevel.Critical, AlarmType.CDUStatus,
                "CDU处于告警状态", (int)data.CDUStatus, (int)CDUStatus.Warning);
        }
        else if (data.CDUStatus == CDUStatus.Offline)
        {
            TriggerAlarm(data, AlarmLevel.Emergency, AlarmType.CDUStatus,
                "CDU离线", (int)data.CDUStatus, (int)CDUStatus.Normal);
        }
        return Task.CompletedTask;
    }

    private void TriggerAlarm(CoolingUnitData data, AlarmLevel level, AlarmType type,
        string message, double currentValue, double threshold)
    {
        var alarm = new AlarmRecord
        {
            Id = _nextAlarmId++,
            Timestamp = data.Timestamp,
            UnitId = data.UnitId,
            Level = level,
            Type = type,
            Message = message,
            CurrentValue = currentValue,
            Threshold = threshold,
            Acknowledged = false
        };

        lock (_alarmHistory)
        {
            _alarmHistory.Add(alarm);
            if (_alarmHistory.Count > 10000)
            {
                _alarmHistory.RemoveRange(0, 1000);
            }
        }

        _logger.LogWarning("Alarm triggered: {Level} - {Message}", level, message);
        AlarmTriggered?.Invoke(this, alarm);
    }

    public Task<IEnumerable<AlarmRecord>> GetActiveAlarmsAsync()
    {
        lock (_alarmHistory)
        {
            var result = _alarmHistory
                .Where(a => !a.Acknowledged)
                .OrderByDescending(a => a.Timestamp)
                .ToList();

            return Task.FromResult<IEnumerable<AlarmRecord>>(result);
        }
    }

    public Task<IEnumerable<AlarmRecord>> GetAlarmHistoryAsync(DateTime startTime, DateTime endTime)
    {
        lock (_alarmHistory)
        {
            var result = _alarmHistory
                .Where(a => a.Timestamp >= startTime && a.Timestamp <= endTime)
                .OrderByDescending(a => a.Timestamp)
                .ToList();

            return Task.FromResult<IEnumerable<AlarmRecord>>(result);
        }
    }

    public Task AcknowledgeAlarmAsync(long alarmId, string acknowledgedBy)
    {
        lock (_alarmHistory)
        {
            var alarm = _alarmHistory.FirstOrDefault(a => a.Id == alarmId);
            if (alarm != null)
            {
                alarm.Acknowledged = true;
                alarm.AcknowledgedBy = acknowledgedBy;
                alarm.AcknowledgedAt = DateTime.Now;
                _logger.LogInformation("Alarm {AlarmId} acknowledged by {User}", alarmId, acknowledgedBy);
            }
        }
        return Task.CompletedTask;
    }

    public void ConfigureThresholds(AlarmThresholds thresholds)
    {
        _thresholds = thresholds;
        _logger.LogInformation("Alarm thresholds updated");
    }
}
