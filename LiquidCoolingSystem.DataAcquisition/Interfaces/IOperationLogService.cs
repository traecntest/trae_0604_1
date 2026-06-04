using LiquidCoolingSystem.DataAcquisition.Models;

namespace LiquidCoolingSystem.DataAcquisition.Interfaces;

public interface IOperationLogService
{
    Task LogOperationAsync(string userName, string operationType, string description, string ipAddress = "");
    Task<IEnumerable<OperationLog>> GetLogsAsync(DateTime startTime, DateTime endTime, string? userName = null);
    Task<IEnumerable<OperationLog>> GetRecentLogsAsync(int count = 100);
}
