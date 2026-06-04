using LiquidCoolingSystem.BusinessLogic.Interfaces;
using LiquidCoolingSystem.BusinessLogic.Models;
using LiquidCoolingSystem.DataAcquisition.Interfaces;
using LiquidCoolingSystem.DataAcquisition.Models;
using Microsoft.Extensions.Logging;

namespace LiquidCoolingSystem.BusinessLogic.Services;

public class EnergyEfficiencyAnalyzer : IEnergyEfficiencyAnalyzer
{
    private readonly ILogger<EnergyEfficiencyAnalyzer> _logger;
    private readonly ITimeSeriesDatabase _timeSeriesDb;

    public EnergyEfficiencyAnalyzer(ILogger<EnergyEfficiencyAnalyzer> logger, ITimeSeriesDatabase timeSeriesDb)
    {
        _logger = logger;
        _timeSeriesDb = timeSeriesDb;
    }

    public async Task<EnergyEfficiencyReport> GenerateReportAsync(DateTime startTime, DateTime endTime)
    {
        var unitIds = new[] { "CU-001", "CU-002", "CU-003", "CU-004" };
        var allData = new List<CoolingUnitData>();
        var unitEfficiencies = new Dictionary<string, UnitEfficiency>();

        foreach (var unitId in unitIds)
        {
            var unitData = await _timeSeriesDb.QueryDataAsync(unitId, startTime, endTime);
            var dataList = unitData.ToList();
            allData.AddRange(dataList);

            if (dataList.Any())
            {
                unitEfficiencies[unitId] = new UnitEfficiency
                {
                    UnitId = unitId,
                    AveragePUE = dataList.Average(d => d.PUE),
                    EnergyConsumption = dataList.Sum(d => d.CabinetPowerDensity * 0.01),
                    CoolingEnergy = dataList.Sum(d => (d.PumpSpeed * 0.01 + d.FanFrequency * 0.005))
                };
            }
        }

        var report = new EnergyEfficiencyReport
        {
            ReportTime = DateTime.Now,
            Duration = endTime - startTime,
            AveragePUE = allData.Any() ? allData.Average(d => d.PUE) : 0,
            MinPUE = allData.Any() ? allData.Min(d => d.PUE) : 0,
            MaxPUE = allData.Any() ? allData.Max(d => d.PUE) : 0,
            TotalEnergySaved = CalculateEnergySavings(allData),
            CoolingEfficiency = await CalculateCoolingEfficiencyAsync(allData),
            UnitEfficiencies = unitEfficiencies,
            Recommendations = (await GetOptimizationRecommendationsAsync(allData)).ToList()
        };

        _logger.LogInformation("Energy efficiency report generated: Avg PUE={AvgPUE:F3}", report.AveragePUE);
        return report;
    }

    public async Task<EnergyEfficiencyReport> GenerateReportFromCurrentDataAsync(
        IEnumerable<CoolingUnitData> currentData, IEnumerable<CoolingUnitData> historicalData)
    {
        var allCurrent = currentData.ToList();
        var allHistorical = historicalData.ToList();
        var allData = allHistorical.Concat(allCurrent).ToList();

        var unitEfficiencies = new Dictionary<string, UnitEfficiency>();

        foreach (var group in allData.GroupBy(d => d.UnitId))
        {
            var groupList = group.ToList();
            unitEfficiencies[group.Key] = new UnitEfficiency
            {
                UnitId = group.Key,
                AveragePUE = groupList.Average(d => d.PUE),
                EnergyConsumption = groupList.Sum(d => d.CabinetPowerDensity * 0.01),
                CoolingEnergy = groupList.Sum(d => (d.PumpSpeed * 0.01 + d.FanFrequency * 0.005))
            };
        }

        var report = new EnergyEfficiencyReport
        {
            ReportTime = DateTime.Now,
            Duration = allHistorical.Any()
                ? allHistorical.Max(d => d.Timestamp) - allHistorical.Min(d => d.Timestamp)
                : TimeSpan.FromMinutes(5),
            AveragePUE = allData.Any() ? allData.Average(d => d.PUE) : 0,
            MinPUE = allData.Any() ? allData.Min(d => d.PUE) : 0,
            MaxPUE = allData.Any() ? allData.Max(d => d.PUE) : 0,
            TotalEnergySaved = CalculateEnergySavings(allData),
            CoolingEfficiency = await CalculateCoolingEfficiencyAsync(allData),
            UnitEfficiencies = unitEfficiencies,
            Recommendations = (await GetOptimizationRecommendationsAsync(allData)).ToList()
        };

        _logger.LogInformation("Report from current data: Avg PUE={AvgPUE:F3}, DataPoints={Count}",
            report.AveragePUE, allData.Count);
        return report;
    }

    public Task<double> CalculatePUEAsync(IEnumerable<CoolingUnitData> data)
    {
        var dataList = data.ToList();
        var result = dataList.Any() ? dataList.Average(d => d.PUE) : 0;
        return Task.FromResult(result);
    }

    public Task<double> CalculateCoolingEfficiencyAsync(IEnumerable<CoolingUnitData> data)
    {
        var dataList = data.ToList();
        if (!dataList.Any()) return Task.FromResult(0.0);

        var avgPUE = dataList.Average(d => d.PUE);
        var efficiency = 100 / avgPUE;
        return Task.FromResult(Math.Round(efficiency, 2));
    }

    public Task<IEnumerable<string>> GetOptimizationRecommendationsAsync(IEnumerable<CoolingUnitData> recentData)
    {
        var recommendations = new List<string>();
        var dataList = recentData.ToList();

        if (!dataList.Any())
        {
            recommendations.Add("数据不足，无法生成优化建议");
            return Task.FromResult<IEnumerable<string>>(recommendations);
        }

        var avgPUE = dataList.Average(d => d.PUE);
        var avgChipTemp = dataList.Average(d => d.ChipJunctionTemperature);
        var avgLoad = dataList.Average(d => d.ComputingLoad);

        if (avgPUE > 1.25)
        {
            recommendations.Add($"PUE值偏高({avgPUE:F3})，建议优化冷却水流量和冷却塔运行策略");
        }

        if (avgChipTemp < 65 && avgLoad < 60)
        {
            recommendations.Add($"芯片温度偏低({avgChipTemp:F1}°C)且负载较低，建议降低泵速以节能");
        }

        if (dataList.Any(d => d.ReturnTemperature - d.SupplyTemperature < 5))
        {
            recommendations.Add("部分机组冷却温差过小，可能存在过度冷却现象");
        }

        if (dataList.GroupBy(d => d.UnitId)
            .Select(g => g.Average(d => d.FanFrequency))
            .Any(f => f > 60))
        {
            recommendations.Add("风扇运行频率较高，建议检查室外湿球温度影响");
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add("系统运行状态良好，当前配置已优化");
        }

        return Task.FromResult<IEnumerable<string>>(recommendations);
    }

    private double CalculateEnergySavings(List<CoolingUnitData> allData)
    {
        if (!allData.Any()) return 0;

        var baselinePUE = 1.3;
        var actualEnergy = allData.Sum(d => d.CabinetPowerDensity * 0.01);
        var baselineEnergy = actualEnergy * baselinePUE;
        var actualTotalEnergy = actualEnergy * allData.Average(d => d.PUE);

        return Math.Round(baselineEnergy - actualTotalEnergy, 2);
    }
}
