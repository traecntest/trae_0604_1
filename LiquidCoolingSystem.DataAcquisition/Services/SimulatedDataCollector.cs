using LiquidCoolingSystem.DataAcquisition.Interfaces;
using LiquidCoolingSystem.DataAcquisition.Models;
using Microsoft.Extensions.Logging;

namespace LiquidCoolingSystem.DataAcquisition.Services;

public class SimulatedDataCollector : IDataCollector
{
    private readonly ILogger<SimulatedDataCollector> _logger;
    private readonly Random _random;
    private readonly Dictionary<string, CoolingUnitData> _lastData;
    private readonly string[] _unitIds = { "CU-001", "CU-002", "CU-003", "CU-004" };

    public event EventHandler<CoolingUnitData>? DataCollected;

    public SimulatedDataCollector(ILogger<SimulatedDataCollector> logger)
    {
        _logger = logger;
        _random = new Random();
        _lastData = new Dictionary<string, CoolingUnitData>();
        InitializeLastData();
    }

    private void InitializeLastData()
    {
        foreach (var unitId in _unitIds)
        {
            _lastData[unitId] = new CoolingUnitData
            {
                UnitId = unitId,
                Timestamp = DateTime.Now,
                CoolantFlowRate = 120 + _random.NextDouble() * 30,
                SupplyTemperature = 18 + _random.NextDouble() * 4,
                ReturnTemperature = 28 + _random.NextDouble() * 5,
                ColdPlatePressureDrop = 15 + _random.NextDouble() * 5,
                PUE = 1.15 + _random.NextDouble() * 0.15,
                OutdoorWetBulbTemp = 22 + _random.NextDouble() * 8,
                CDUStatus = CDUStatus.Normal,
                CabinetPowerDensity = 25 + _random.NextDouble() * 15,
                ChipJunctionTemperature = 65 + _random.NextDouble() * 15,
                PumpSpeed = 75 + _random.NextDouble() * 15,
                FanFrequency = 45 + _random.NextDouble() * 20,
                ComputingLoad = 60 + _random.NextDouble() * 30
            };
        }
    }

    public async Task<CoolingUnitData> CollectDataAsync(string unitId)
    {
        if (!_lastData.TryGetValue(unitId, out var lastData))
        {
            throw new ArgumentException($"Unknown unit ID: {unitId}");
        }

        await Task.Delay(_random.Next(10, 50));

        var newData = new CoolingUnitData
        {
            UnitId = unitId,
            Timestamp = DateTime.Now,
            CoolantFlowRate = VaryValue(lastData.CoolantFlowRate, 100, 160, 2),
            SupplyTemperature = VaryValue(lastData.SupplyTemperature, 15, 25, 0.5),
            ReturnTemperature = VaryValue(lastData.ReturnTemperature, 25, 38, 0.8),
            ColdPlatePressureDrop = VaryValue(lastData.ColdPlatePressureDrop, 10, 25, 1),
            PUE = VaryValue(lastData.PUE, 1.08, 1.35, 0.02),
            OutdoorWetBulbTemp = VaryValue(lastData.OutdoorWetBulbTemp, 18, 35, 1),
            CabinetPowerDensity = VaryValue(lastData.CabinetPowerDensity, 15, 50, 3),
            ChipJunctionTemperature = VaryValue(lastData.ChipJunctionTemperature, 55, 90, 2),
            PumpSpeed = VaryValue(lastData.PumpSpeed, 50, 100, 3),
            FanFrequency = VaryValue(lastData.FanFrequency, 30, 70, 4),
            ComputingLoad = VaryValue(lastData.ComputingLoad, 30, 95, 5)
        };

        newData.CDUStatus = DetermineCDUStatus(newData);
        _lastData[unitId] = newData;

        _logger.LogDebug("Collected data for unit {UnitId}: PUE={PUE:F2}, TempDelta={TempDelta:F1}°C",
            unitId, newData.PUE, newData.ReturnTemperature - newData.SupplyTemperature);

        DataCollected?.Invoke(this, newData);

        return newData;
    }

    public async Task<IEnumerable<CoolingUnitData>> CollectAllUnitsAsync()
    {
        var tasks = _unitIds.Select(CollectDataAsync);
        var results = await Task.WhenAll(tasks);
        return results;
    }

    private double VaryValue(double currentValue, double min, double max, double maxChange)
    {
        var change = (_random.NextDouble() - 0.5) * 2 * maxChange;
        var newValue = currentValue + change;
        return Math.Clamp(newValue, min, max);
    }

    private CDUStatus DetermineCDUStatus(CoolingUnitData data)
    {
        var tempDelta = data.ReturnTemperature - data.SupplyTemperature;
        var isWarning = data.ChipJunctionTemperature > 80 ||
                        data.CabinetPowerDensity > 40 ||
                        tempDelta > 12;

        var isAlarm = data.ChipJunctionTemperature > 88 ||
                      data.CabinetPowerDensity > 48 ||
                      tempDelta > 15;

        if (isAlarm) return CDUStatus.Alarm;
        if (isWarning) return CDUStatus.Warning;
        return CDUStatus.Normal;
    }

    public IEnumerable<string> GetUnitIds() => _unitIds;
}
