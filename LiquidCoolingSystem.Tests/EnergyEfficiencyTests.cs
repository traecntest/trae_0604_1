using Microsoft.Extensions.Logging.Abstractions;
using LiquidCoolingSystem.DataAcquisition.Models;
using LiquidCoolingSystem.DataAcquisition.Services;
using LiquidCoolingSystem.BusinessLogic.Models;
using LiquidCoolingSystem.BusinessLogic.Services;

namespace LiquidCoolingSystem.Tests;

public class EnergyEfficiencyTests
{
    [Fact]
    public async Task EnergyEfficiencyAnalyzer_CalculatePUEAsync_ReturnsAverage()
    {
        var dbLogger = NullLogger<InfluxDbWriter>.Instance;
        var db = new InfluxDbWriter(dbLogger);
        var logger = NullLogger<EnergyEfficiencyAnalyzer>.Instance;
        var analyzer = new EnergyEfficiencyAnalyzer(logger, db);

        var data = new[]
        {
            new CoolingUnitData { PUE = 1.1 },
            new CoolingUnitData { PUE = 1.2 },
            new CoolingUnitData { PUE = 1.3 }
        };

        var result = await analyzer.CalculatePUEAsync(data);

        Assert.Equal(1.2, result, 2);
    }

    [Fact]
    public async Task EnergyEfficiencyAnalyzer_CalculateCoolingEfficiencyAsync_ReturnsEfficiency()
    {
        var dbLogger = NullLogger<InfluxDbWriter>.Instance;
        var db = new InfluxDbWriter(dbLogger);
        var logger = NullLogger<EnergyEfficiencyAnalyzer>.Instance;
        var analyzer = new EnergyEfficiencyAnalyzer(logger, db);

        var data = new[]
        {
            new CoolingUnitData { PUE = 1.25 }
        };

        var result = await analyzer.CalculateCoolingEfficiencyAsync(data);

        Assert.Equal(80, result, 2);
    }

    [Fact]
    public async Task EnergyEfficiencyAnalyzer_GetOptimizationRecommendationsAsync_ReturnsRecommendations()
    {
        var dbLogger = NullLogger<InfluxDbWriter>.Instance;
        var db = new InfluxDbWriter(dbLogger);
        var logger = NullLogger<EnergyEfficiencyAnalyzer>.Instance;
        var analyzer = new EnergyEfficiencyAnalyzer(logger, db);

        var data = new[]
        {
            new CoolingUnitData
            {
                UnitId = "CU-001",
                PUE = 1.15,
                ChipJunctionTemperature = 72,
                ComputingLoad = 65,
                ReturnTemperature = 28,
                SupplyTemperature = 18,
                FanFrequency = 55
            }
        };

        var result = await analyzer.GetOptimizationRecommendationsAsync(data);

        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task EnergyEfficiencyAnalyzer_EmptyData_ReturnsEmptyPUE()
    {
        var dbLogger = NullLogger<InfluxDbWriter>.Instance;
        var db = new InfluxDbWriter(dbLogger);
        var logger = NullLogger<EnergyEfficiencyAnalyzer>.Instance;
        var analyzer = new EnergyEfficiencyAnalyzer(logger, db);

        var result = await analyzer.CalculatePUEAsync(Enumerable.Empty<CoolingUnitData>());

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task EnergyEfficiencyAnalyzer_GenerateReportAsync_ReturnsValidReport()
    {
        var dbLogger = NullLogger<InfluxDbWriter>.Instance;
        var db = new InfluxDbWriter(dbLogger);
        var logger = NullLogger<EnergyEfficiencyAnalyzer>.Instance;
        var analyzer = new EnergyEfficiencyAnalyzer(logger, db);

        var startTime = DateTime.Now.AddHours(-1);
        var endTime = DateTime.Now;

        var report = await analyzer.GenerateReportAsync(startTime, endTime);

        Assert.NotNull(report);
        Assert.True(report.ReportTime <= DateTime.Now);
        Assert.Equal(endTime - startTime, report.Duration);
        Assert.NotNull(report.UnitEfficiencies);
        Assert.NotNull(report.Recommendations);
    }

    [Fact]
    public async Task EnergyEfficiencyAnalyzer_HighPUE_GeneratesOptimizationRecommendation()
    {
        var dbLogger = NullLogger<InfluxDbWriter>.Instance;
        var db = new InfluxDbWriter(dbLogger);
        var logger = NullLogger<EnergyEfficiencyAnalyzer>.Instance;
        var analyzer = new EnergyEfficiencyAnalyzer(logger, db);

        var data = new[]
        {
            new CoolingUnitData
            {
                UnitId = "CU-001",
                PUE = 1.3,
                ChipJunctionTemperature = 72,
                ComputingLoad = 65,
                ReturnTemperature = 28,
                SupplyTemperature = 18
            }
        };

        var recommendations = await analyzer.GetOptimizationRecommendationsAsync(data);

        Assert.Contains(recommendations, r => r.Contains("PUE"));
    }

    [Fact]
    public async Task EnergyEfficiencyAnalyzer_LowTemperatureAndLoad_GeneratesEnergySavingRecommendation()
    {
        var dbLogger = NullLogger<InfluxDbWriter>.Instance;
        var db = new InfluxDbWriter(dbLogger);
        var logger = NullLogger<EnergyEfficiencyAnalyzer>.Instance;
        var analyzer = new EnergyEfficiencyAnalyzer(logger, db);

        var data = new[]
        {
            new CoolingUnitData
            {
                UnitId = "CU-001",
                PUE = 1.15,
                ChipJunctionTemperature = 60,
                ComputingLoad = 40,
                ReturnTemperature = 28,
                SupplyTemperature = 18
            }
        };

        var recommendations = await analyzer.GetOptimizationRecommendationsAsync(data);

        Assert.Contains(recommendations, r => r.Contains("节能"));
    }

    [Fact]
    public void UnitEfficiency_DefaultValues_AreCorrect()
    {
        var efficiency = new UnitEfficiency();

        Assert.Equal(string.Empty, efficiency.UnitId);
        Assert.Equal(0, efficiency.AveragePUE);
        Assert.Equal(0, efficiency.EnergyConsumption);
        Assert.Equal(0, efficiency.CoolingEnergy);
    }

    [Fact]
    public void EnergyEfficiencyReport_DefaultValues_AreCorrect()
    {
        var report = new EnergyEfficiencyReport();

        Assert.Equal(default, report.ReportTime);
        Assert.Equal(TimeSpan.Zero, report.Duration);
        Assert.Equal(0, report.AveragePUE);
        Assert.NotNull(report.UnitEfficiencies);
        Assert.NotNull(report.Recommendations);
    }

    [Fact]
    public async Task GenerateReportFromCurrentDataAsync_WithCurrentData_ReturnsReport()
    {
        var dbLogger = NullLogger<InfluxDbWriter>.Instance;
        var db = new InfluxDbWriter(dbLogger);
        var logger = NullLogger<EnergyEfficiencyAnalyzer>.Instance;
        var analyzer = new EnergyEfficiencyAnalyzer(logger, db);

        var currentData = new[]
        {
            new CoolingUnitData { UnitId = "CU-001", PUE = 1.15, ChipJunctionTemperature = 70, CabinetPowerDensity = 30, PumpSpeed = 75, FanFrequency = 45, ComputingLoad = 60, ReturnTemperature = 28, SupplyTemperature = 18, Timestamp = DateTime.Now },
            new CoolingUnitData { UnitId = "CU-002", PUE = 1.22, ChipJunctionTemperature = 72, CabinetPowerDensity = 32, PumpSpeed = 78, FanFrequency = 48, ComputingLoad = 65, ReturnTemperature = 29, SupplyTemperature = 19, Timestamp = DateTime.Now }
        };

        var historicalData = new[]
        {
            new CoolingUnitData { UnitId = "CU-001", PUE = 1.12, ChipJunctionTemperature = 68, CabinetPowerDensity = 28, PumpSpeed = 72, FanFrequency = 42, ComputingLoad = 55, ReturnTemperature = 27, SupplyTemperature = 17, Timestamp = DateTime.Now.AddMinutes(-10) },
            new CoolingUnitData { UnitId = "CU-002", PUE = 1.18, ChipJunctionTemperature = 70, CabinetPowerDensity = 30, PumpSpeed = 74, FanFrequency = 44, ComputingLoad = 58, ReturnTemperature = 28, SupplyTemperature = 18, Timestamp = DateTime.Now.AddMinutes(-10) }
        };

        var report = await analyzer.GenerateReportFromCurrentDataAsync(currentData, historicalData);

        Assert.NotNull(report);
        Assert.True(report.AveragePUE > 0);
        Assert.True(report.MinPUE > 0);
        Assert.True(report.MaxPUE > 0);
        Assert.True(report.CoolingEfficiency > 0);
        Assert.Equal(2, report.UnitEfficiencies.Count);
        Assert.Contains(report.UnitEfficiencies, u => u.Key == "CU-001");
        Assert.Contains(report.UnitEfficiencies, u => u.Key == "CU-002");
    }

    [Fact]
    public async Task GenerateReportFromCurrentDataAsync_EmptyData_ReturnsZeroReport()
    {
        var dbLogger = NullLogger<InfluxDbWriter>.Instance;
        var db = new InfluxDbWriter(dbLogger);
        var logger = NullLogger<EnergyEfficiencyAnalyzer>.Instance;
        var analyzer = new EnergyEfficiencyAnalyzer(logger, db);

        var report = await analyzer.GenerateReportFromCurrentDataAsync(
            Enumerable.Empty<CoolingUnitData>(), Enumerable.Empty<CoolingUnitData>());

        Assert.NotNull(report);
        Assert.Equal(0, report.AveragePUE);
        Assert.Empty(report.UnitEfficiencies);
    }
}
