using LiquidCoolingSystem.DataAcquisition.Models;

namespace LiquidCoolingSystem.DataAcquisition.Interfaces;

public interface IDataCollector
{
    Task<CoolingUnitData> CollectDataAsync(string unitId);
    Task<IEnumerable<CoolingUnitData>> CollectAllUnitsAsync();
    event EventHandler<CoolingUnitData>? DataCollected;
}
