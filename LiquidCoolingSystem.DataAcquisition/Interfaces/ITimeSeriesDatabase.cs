using LiquidCoolingSystem.DataAcquisition.Models;

namespace LiquidCoolingSystem.DataAcquisition.Interfaces;

public interface ITimeSeriesDatabase
{
    Task WriteDataAsync(CoolingUnitData data);
    Task WriteBatchDataAsync(IEnumerable<CoolingUnitData> dataList);
    Task<IEnumerable<CoolingUnitData>> QueryDataAsync(string unitId, DateTime startTime, DateTime endTime);
    Task<IEnumerable<CoolingUnitData>> GetLatestDataAsync(string unitId, int count = 100);
    Task<bool> IsConnectedAsync();
}
