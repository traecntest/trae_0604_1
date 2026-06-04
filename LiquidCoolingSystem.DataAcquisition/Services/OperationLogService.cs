using LiquidCoolingSystem.DataAcquisition.Interfaces;
using LiquidCoolingSystem.DataAcquisition.Models;
using Microsoft.Extensions.Logging;

namespace LiquidCoolingSystem.DataAcquisition.Services;

public class OperationLogService : IOperationLogService
{
    private readonly ILogger<OperationLogService> _logger;
    private readonly List<OperationLog> _logs;
    private long _nextId;

    public OperationLogService(ILogger<OperationLogService> logger)
    {
        _logger = logger;
        _logs = new List<OperationLog>();
        _nextId = 1;
    }

    public Task LogOperationAsync(string userName, string operationType, string description, string ipAddress = "")
    {
        var log = new OperationLog
        {
            Id = _nextId++,
            Timestamp = DateTime.Now,
            UserName = userName,
            OperationType = operationType,
            Description = description,
            IpAddress = ipAddress
        };

        lock (_logs)
        {
            _logs.Add(log);
            if (_logs.Count > 10000)
            {
                _logs.RemoveRange(0, 1000);
            }
        }

        _logger.LogInformation("Operation Log: {User} - {Type} - {Desc}", userName, operationType, description);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<OperationLog>> GetLogsAsync(DateTime startTime, DateTime endTime, string? userName = null)
    {
        lock (_logs)
        {
            var query = _logs
                .Where(l => l.Timestamp >= startTime && l.Timestamp <= endTime);

            if (!string.IsNullOrEmpty(userName))
            {
                query = query.Where(l => l.UserName == userName);
            }

            var result = query
                .OrderByDescending(l => l.Timestamp)
                .ToList();

            return Task.FromResult<IEnumerable<OperationLog>>(result);
        }
    }

    public Task<IEnumerable<OperationLog>> GetRecentLogsAsync(int count = 100)
    {
        lock (_logs)
        {
            var result = _logs
                .OrderByDescending(l => l.Timestamp)
                .Take(count)
                .ToList();

            return Task.FromResult<IEnumerable<OperationLog>>(result);
        }
    }
}
