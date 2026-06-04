using Microsoft.Extensions.Logging.Abstractions;
using LiquidCoolingSystem.DataAcquisition.Models;
using LiquidCoolingSystem.BusinessLogic.Models;
using LiquidCoolingSystem.BusinessLogic.Services;
using LiquidCoolingSystem.BusinessLogic.Interfaces;

namespace LiquidCoolingSystem.Tests;

public class BusinessLogicTests
{
    [Fact]
    public async Task AlarmEngine_AnalyzeDataAsync_HighPowerDensity_TriggersAlarm()
    {
        var logger = NullLogger<AlarmEngine>.Instance;
        var engine = new AlarmEngine(logger);
        AlarmRecord? receivedAlarm = null;
        engine.AlarmTriggered += (s, e) => receivedAlarm = e;

        var data = new CoolingUnitData
        {
            UnitId = "CU-001",
            Timestamp = DateTime.Now,
            CabinetPowerDensity = 50,
            ChipJunctionTemperature = 70,
            ReturnTemperature = 28,
            SupplyTemperature = 18,
            PUE = 1.2,
            CDUStatus = CDUStatus.Normal
        };

        await engine.AnalyzeDataAsync(data);

        Assert.NotNull(receivedAlarm);
        Assert.Equal(AlarmType.PowerDensity, receivedAlarm.Type);
        Assert.Equal(AlarmLevel.Emergency, receivedAlarm.Level);
        Assert.False(receivedAlarm.Acknowledged);
    }

    [Fact]
    public async Task AlarmEngine_AnalyzeDataAsync_HighChipTemp_TriggersAlarm()
    {
        var logger = NullLogger<AlarmEngine>.Instance;
        var engine = new AlarmEngine(logger);
        var alarms = new List<AlarmRecord>();
        engine.AlarmTriggered += (s, e) => alarms.Add(e);

        var data = new CoolingUnitData
        {
            UnitId = "CU-001",
            Timestamp = DateTime.Now,
            CabinetPowerDensity = 30,
            ChipJunctionTemperature = 85,
            ReturnTemperature = 28,
            SupplyTemperature = 18,
            PUE = 1.2,
            CDUStatus = CDUStatus.Normal
        };

        await engine.AnalyzeDataAsync(data);

        Assert.Contains(alarms, a => a.Type == AlarmType.ChipTemperature);
    }

    [Fact]
    public async Task AlarmEngine_AnalyzeDataAsync_HighTempDelta_TriggersAlarm()
    {
        var logger = NullLogger<AlarmEngine>.Instance;
        var engine = new AlarmEngine(logger);
        var alarms = new List<AlarmRecord>();
        engine.AlarmTriggered += (s, e) => alarms.Add(e);

        var data = new CoolingUnitData
        {
            UnitId = "CU-001",
            Timestamp = DateTime.Now,
            CabinetPowerDensity = 30,
            ChipJunctionTemperature = 70,
            ReturnTemperature = 35,
            SupplyTemperature = 18,
            PUE = 1.2,
            CDUStatus = CDUStatus.Normal
        };

        await engine.AnalyzeDataAsync(data);

        Assert.Contains(alarms, a => a.Type == AlarmType.TemperatureDelta);
    }

    [Fact]
    public async Task AlarmEngine_GetActiveAlarmsAsync_ReturnsUnacknowledged()
    {
        var logger = NullLogger<AlarmEngine>.Instance;
        var engine = new AlarmEngine(logger);

        var data = new CoolingUnitData
        {
            UnitId = "CU-001",
            Timestamp = DateTime.Now,
            CabinetPowerDensity = 50,
            ChipJunctionTemperature = 70,
            ReturnTemperature = 28,
            SupplyTemperature = 18,
            PUE = 1.2,
            CDUStatus = CDUStatus.Normal
        };

        await engine.AnalyzeDataAsync(data);

        var activeAlarms = await engine.GetActiveAlarmsAsync();
        Assert.NotEmpty(activeAlarms);
        Assert.All(activeAlarms, a => Assert.False(a.Acknowledged));
    }

    [Fact]
    public async Task AlarmEngine_AcknowledgeAlarmAsync_Works()
    {
        var logger = NullLogger<AlarmEngine>.Instance;
        var engine = new AlarmEngine(logger);
        long alarmId = 0;
        engine.AlarmTriggered += (s, e) => alarmId = e.Id;

        var data = new CoolingUnitData
        {
            UnitId = "CU-001",
            Timestamp = DateTime.Now,
            CabinetPowerDensity = 50,
            ChipJunctionTemperature = 70,
            ReturnTemperature = 28,
            SupplyTemperature = 18,
            PUE = 1.2,
            CDUStatus = CDUStatus.Normal
        };

        await engine.AnalyzeDataAsync(data);
        await engine.AcknowledgeAlarmAsync(alarmId, "TestUser");

        var activeAlarms = await engine.GetActiveAlarmsAsync();
        Assert.DoesNotContain(activeAlarms, a => a.Id == alarmId && !a.Acknowledged);
    }

    [Fact]
    public async Task CoolingControlSystem_ComputeControlActionAsync_ReturnsValidCommand()
    {
        var logger = NullLogger<CoolingControlSystem>.Instance;
        var control = new CoolingControlSystem(logger);

        var data = new CoolingUnitData
        {
            UnitId = "CU-001",
            Timestamp = DateTime.Now,
            ChipJunctionTemperature = 80,
            ComputingLoad = 70,
            OutdoorWetBulbTemp = 25
        };

        var command = await control.ComputeControlActionAsync(data);

        Assert.NotNull(command);
        Assert.Equal("CU-001", command.UnitId);
        Assert.InRange(command.TargetPumpSpeed, 40, 100);
        Assert.InRange(command.TargetFanFrequency, 20, 75);
    }

    [Fact]
    public async Task CoolingControlSystem_HighTemperature_IncreasesPumpSpeed()
    {
        var logger = NullLogger<CoolingControlSystem>.Instance;
        var control = new CoolingControlSystem(logger);

        var normalData = new CoolingUnitData
        {
            UnitId = "CU-001",
            ChipJunctionTemperature = 70,
            ComputingLoad = 50
        };
        var normalCommand = await control.ComputeControlActionAsync(normalData);

        var highTempData = new CoolingUnitData
        {
            UnitId = "CU-001",
            ChipJunctionTemperature = 85,
            ComputingLoad = 90
        };
        var highTempCommand = await control.ComputeControlActionAsync(highTempData);

        Assert.True(highTempCommand.TargetPumpSpeed >= normalCommand.TargetPumpSpeed);
    }

    [Fact]
    public void CoolingControlSystem_GetTargetChipTemperature_ReturnsDefault()
    {
        var logger = NullLogger<CoolingControlSystem>.Instance;
        var control = new CoolingControlSystem(logger);

        var target = control.GetTargetChipTemperature();

        Assert.Equal(75, target);
    }

    [Fact]
    public void AlarmThresholds_DefaultValues_AreCorrect()
    {
        var thresholds = new AlarmThresholds();

        Assert.Equal(40, thresholds.WarningPowerDensity);
        Assert.Equal(48, thresholds.CriticalPowerDensity);
        Assert.Equal(12, thresholds.WarningTempDelta);
        Assert.Equal(15, thresholds.CriticalTempDelta);
        Assert.Equal(80, thresholds.WarningChipTemp);
        Assert.Equal(88, thresholds.CriticalChipTemp);
    }

    [Fact]
    public void ControlParameters_DefaultValues_AreCorrect()
    {
        var parameters = new ControlParameters();

        Assert.Equal(75, parameters.TargetChipTemperature);
        Assert.Equal(40, parameters.MinPumpSpeed);
        Assert.Equal(100, parameters.MaxPumpSpeed);
        Assert.Equal(20, parameters.MinFanFrequency);
        Assert.Equal(75, parameters.MaxFanFrequency);
    }
}
