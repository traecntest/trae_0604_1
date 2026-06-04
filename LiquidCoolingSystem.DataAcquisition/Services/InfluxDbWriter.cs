using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using LiquidCoolingSystem.DataAcquisition.Interfaces;
using LiquidCoolingSystem.DataAcquisition.Models;
using Microsoft.Extensions.Logging;

namespace LiquidCoolingSystem.DataAcquisition.Services;

public class InfluxDbWriter : ITimeSeriesDatabase
{
    private readonly ILogger<InfluxDbWriter> _logger;
    private readonly InfluxDBClient? _client;
    private readonly string _bucket;
    private readonly string _org;
    private readonly bool _useSimulation;

    private readonly List<CoolingUnitData> _simulatedStorage;

    public InfluxDbWriter(ILogger<InfluxDbWriter> logger)
    {
        _logger = logger;
        _useSimulation = true;
        _simulatedStorage = new List<CoolingUnitData>();

        _bucket = "liquid_cooling";
        _org = "datacenter";

        try
        {
            _client = new InfluxDBClient("http://localhost:8086", "my-token");
            _logger.LogInformation("InfluxDB client initialized");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to connect to InfluxDB, using simulation mode");
            _useSimulation = true;
        }
    }

    public async Task WriteDataAsync(CoolingUnitData data)
    {
        if (_useSimulation)
        {
            lock (_simulatedStorage)
            {
                _simulatedStorage.Add(data);
                if (_simulatedStorage.Count > 10000)
                {
                    _simulatedStorage.RemoveRange(0, 1000);
                }
            }

            _logger.LogDebug("Simulated write: {UnitId} at {Timestamp}", data.UnitId, data.Timestamp);
            await Task.CompletedTask;
            return;
        }

        try
        {
            var point = PointData.Measurement("cooling_unit")
                .Tag("unit_id", data.UnitId)
                .Field("coolant_flow_rate", data.CoolantFlowRate)
                .Field("supply_temperature", data.SupplyTemperature)
                .Field("return_temperature", data.ReturnTemperature)
                .Field("cold_plate_pressure_drop", data.ColdPlatePressureDrop)
                .Field("pue", data.PUE)
                .Field("outdoor_wet_bulb_temp", data.OutdoorWetBulbTemp)
                .Field("cdu_status", (int)data.CDUStatus)
                .Field("cabinet_power_density", data.CabinetPowerDensity)
                .Field("chip_junction_temp", data.ChipJunctionTemperature)
                .Field("pump_speed", data.PumpSpeed)
                .Field("fan_frequency", data.FanFrequency)
                .Field("computing_load", data.ComputingLoad)
                .Timestamp(data.Timestamp, WritePrecision.Ns);

            var writeApi = _client!.GetWriteApiAsync();
            await writeApi.WritePointAsync(point, _bucket, _org);

            _logger.LogDebug("Written data to InfluxDB: {UnitId}", data.UnitId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write data to InfluxDB");
            throw;
        }
    }

    public async Task WriteBatchDataAsync(IEnumerable<CoolingUnitData> dataList)
    {
        foreach (var data in dataList)
        {
            await WriteDataAsync(data);
        }
    }

    public Task<IEnumerable<CoolingUnitData>> QueryDataAsync(string unitId, DateTime startTime, DateTime endTime)
    {
        if (_useSimulation)
        {
            lock (_simulatedStorage)
            {
                var result = _simulatedStorage
                    .Where(d => d.UnitId == unitId && d.Timestamp >= startTime && d.Timestamp <= endTime)
                    .OrderBy(d => d.Timestamp)
                    .ToList();

                return Task.FromResult<IEnumerable<CoolingUnitData>>(result);
            }
        }

        return Task.FromResult<IEnumerable<CoolingUnitData>>(new List<CoolingUnitData>());
    }

    public Task<IEnumerable<CoolingUnitData>> GetLatestDataAsync(string unitId, int count = 100)
    {
        if (_useSimulation)
        {
            lock (_simulatedStorage)
            {
                var result = _simulatedStorage
                    .Where(d => d.UnitId == unitId)
                    .OrderByDescending(d => d.Timestamp)
                    .Take(count)
                    .Reverse()
                    .ToList();

                return Task.FromResult<IEnumerable<CoolingUnitData>>(result);
            }
        }

        return Task.FromResult<IEnumerable<CoolingUnitData>>(new List<CoolingUnitData>());
    }

    public Task<bool> IsConnectedAsync()
    {
        return Task.FromResult(_useSimulation || _client != null);
    }
}
