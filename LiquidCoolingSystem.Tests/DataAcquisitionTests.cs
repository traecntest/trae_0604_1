using Microsoft.Extensions.Logging.Abstractions;
using LiquidCoolingSystem.DataAcquisition.Models;
using LiquidCoolingSystem.DataAcquisition.Services;

namespace LiquidCoolingSystem.Tests;

public class DataAcquisitionTests
{
    [Fact]
    public async Task SimulatedDataCollector_CollectDataAsync_ReturnsValidData()
    {
        var logger = NullLogger<SimulatedDataCollector>.Instance;
        var collector = new SimulatedDataCollector(logger);

        var data = await collector.CollectDataAsync("CU-001");

        Assert.NotNull(data);
        Assert.Equal("CU-001", data.UnitId);
        Assert.InRange(data.CoolantFlowRate, 100, 160);
        Assert.InRange(data.SupplyTemperature, 15, 25);
        Assert.InRange(data.ReturnTemperature, 25, 38);
        Assert.InRange(data.PUE, 1.08, 1.35);
        Assert.InRange(data.ChipJunctionTemperature, 55, 90);
        Assert.InRange(data.CabinetPowerDensity, 15, 50);
    }

    [Fact]
    public async Task SimulatedDataCollector_CollectAllUnitsAsync_ReturnsAllUnits()
    {
        var logger = NullLogger<SimulatedDataCollector>.Instance;
        var collector = new SimulatedDataCollector(logger);

        var dataList = await collector.CollectAllUnitsAsync();

        Assert.NotNull(dataList);
        Assert.Equal(4, dataList.Count());
        Assert.Contains(dataList, d => d.UnitId == "CU-001");
        Assert.Contains(dataList, d => d.UnitId == "CU-002");
        Assert.Contains(dataList, d => d.UnitId == "CU-003");
        Assert.Contains(dataList, d => d.UnitId == "CU-004");
    }

    [Fact]
    public async Task SimulatedDataCollector_InvalidUnitId_ThrowsException()
    {
        var logger = NullLogger<SimulatedDataCollector>.Instance;
        var collector = new SimulatedDataCollector(logger);

        await Assert.ThrowsAsync<ArgumentException>(() => collector.CollectDataAsync("INVALID"));
    }

    [Fact]
    public async Task InfluxDbWriter_WriteDataAsync_Success()
    {
        var logger = NullLogger<InfluxDbWriter>.Instance;
        var writer = new InfluxDbWriter(logger);

        var data = new CoolingUnitData
        {
            UnitId = "CU-001",
            Timestamp = DateTime.Now,
            CoolantFlowRate = 120,
            SupplyTemperature = 18,
            ReturnTemperature = 28,
            PUE = 1.2
        };

        await writer.WriteDataAsync(data);

        var isConnected = await writer.IsConnectedAsync();
        Assert.True(isConnected);
    }

    [Fact]
    public async Task InfluxDbWriter_QueryDataAsync_ReturnsData()
    {
        var logger = NullLogger<InfluxDbWriter>.Instance;
        var writer = new InfluxDbWriter(logger);

        var startTime = DateTime.Now.AddMinutes(-10);
        var data = new CoolingUnitData
        {
            UnitId = "CU-001",
            Timestamp = DateTime.Now,
            CoolantFlowRate = 120,
            PUE = 1.2
        };

        await writer.WriteDataAsync(data);

        var result = await writer.QueryDataAsync("CU-001", startTime, DateTime.Now);

        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task OperationLogService_LogOperationAsync_Success()
    {
        var logger = NullLogger<OperationLogService>.Instance;
        var logService = new OperationLogService(logger);

        await logService.LogOperationAsync("Admin", "Test", "Test operation", "127.0.0.1");

        var logs = await logService.GetRecentLogsAsync(10);
        Assert.NotEmpty(logs);
        var log = logs.First();
        Assert.Equal("Admin", log.UserName);
        Assert.Equal("Test", log.OperationType);
        Assert.Equal("Test operation", log.Description);
    }

    [Fact]
    public async Task OperationLogService_GetLogsByTimeRange_Works()
    {
        var logger = NullLogger<OperationLogService>.Instance;
        var logService = new OperationLogService(logger);

        await logService.LogOperationAsync("User1", "Type1", "Desc1");
        await Task.Delay(100);
        var midTime = DateTime.Now;
        await Task.Delay(100);
        await logService.LogOperationAsync("User2", "Type2", "Desc2");

        var startTime = midTime.AddMilliseconds(-50);
        var endTime = DateTime.Now;
        var logs = await logService.GetLogsAsync(startTime, endTime);

        Assert.NotEmpty(logs);
        Assert.All(logs, l => Assert.Equal("User2", l.UserName));
    }

    [Fact]
    public void CoolingUnitData_DefaultValues_AreCorrect()
    {
        var data = new CoolingUnitData();

        Assert.Equal(string.Empty, data.UnitId);
        Assert.Equal(default, data.Timestamp);
        Assert.Equal(0, data.CoolantFlowRate);
        Assert.Equal(0, data.PUE);
        Assert.Equal(CDUStatus.Normal, data.CDUStatus);
    }
}
