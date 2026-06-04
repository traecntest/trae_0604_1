using LiquidCoolingSystem.BusinessLogic.Models;
using LiquidCoolingSystem.DataAcquisition.Models;

namespace LiquidCoolingSystem.BusinessLogic.Interfaces;

public interface IEnergyEfficiencyAnalyzer
{
    Task<EnergyEfficiencyReport> GenerateReportAsync(DateTime startTime, DateTime endTime);
    Task<double> CalculatePUEAsync(IEnumerable<CoolingUnitData> data);
    Task<double> CalculateCoolingEfficiencyAsync(IEnumerable<CoolingUnitData> data);
    Task<IEnumerable<string>> GetOptimizationRecommendationsAsync(IEnumerable<CoolingUnitData> recentData);
}
